using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TimelineManager.Services;

/// <summary>
/// Service for handling errors in playlist operations with graceful recovery and detailed logging.
/// Ensures that playlist failures don't prevent processing of other universes.
/// </summary>
public class PlaylistErrorHandler
{
    private readonly ILogger<PlaylistErrorHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaylistErrorHandler"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public PlaylistErrorHandler(ILogger<PlaylistErrorHandler> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Handles errors during playlist creation with graceful recovery.
    /// </summary>
    /// <param name="playlistName">The name of the playlist that failed.</param>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="context">Additional context about the operation.</param>
    /// <returns>An error result with recovery recommendations.</returns>
    public PlaylistErrorResult HandlePlaylistCreationError(
        string playlistName,
        Exception exception,
        PlaylistOperationContext context)
    {
        var errorResult = new PlaylistErrorResult
        {
            PlaylistName = playlistName,
            ErrorType = ClassifyError(exception),
            OriginalException = exception,
            Context = context,
            Timestamp = DateTime.UtcNow
        };

        // Log the error with appropriate level based on severity
        LogError(errorResult);

        // Determine recovery strategy
        errorResult.RecoveryStrategy = DetermineRecoveryStrategy(errorResult);

        // Generate user-friendly error message
        errorResult.UserFriendlyMessage = GenerateUserFriendlyMessage(errorResult);

        // Determine if processing should continue
        errorResult.ShouldContinueProcessing = ShouldContinueProcessing(errorResult);

        return errorResult;
    }

    /// <summary>
    /// Handles errors during batch playlist operations.
    /// </summary>
    /// <param name="batchErrors">The collection of errors that occurred during batch processing.</param>
    /// <returns>A batch error summary with recommendations.</returns>
    public BatchErrorSummary HandleBatchPlaylistErrors(List<PlaylistErrorResult> batchErrors)
    {
        var summary = new BatchErrorSummary
        {
            TotalErrors = batchErrors.Count,
            Timestamp = DateTime.UtcNow
        };

        if (batchErrors.Count == 0)
        {
            summary.OverallSeverity = ErrorSeverity.None;
            return summary;
        }

        // Categorize errors by type
        var errorsByType = batchErrors.GroupBy(e => e.ErrorType).ToList();
        foreach (var group in errorsByType)
        {
            summary.ErrorsByType[group.Key] = group.Count();
        }

        // Determine overall severity
        summary.OverallSeverity = batchErrors.Max(e => e.Severity);

        // Count critical errors
        summary.CriticalErrors = batchErrors.Count(e => e.Severity == ErrorSeverity.Critical);
        summary.RecoverableErrors = batchErrors.Count(e => e.Severity != ErrorSeverity.Critical);

        // Generate batch recommendations
        summary.BatchRecommendations = GenerateBatchRecommendations(batchErrors);

        // Log batch summary
        LogBatchSummary(summary);

        return summary;
    }

    /// <summary>
    /// Attempts to recover from a playlist operation error.
    /// </summary>
    /// <param name="errorResult">The error result to recover from.</param>
    /// <returns>A recovery attempt result.</returns>
    public PlaylistRecoveryResult AttemptRecovery(PlaylistErrorResult errorResult)
    {
        var recoveryResult = new PlaylistRecoveryResult
        {
            OriginalError = errorResult,
            AttemptTimestamp = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation("Attempting recovery for playlist '{PlaylistName}' using strategy: {Strategy}",
                errorResult.PlaylistName, errorResult.RecoveryStrategy);

            switch (errorResult.RecoveryStrategy)
            {
                case RecoveryStrategy.RetryWithDelay:
                    recoveryResult = AttemptRetryRecovery(errorResult);
                    break;

                case RecoveryStrategy.SkipInvalidItems:
                    recoveryResult = AttemptSkipInvalidItemsRecovery(errorResult);
                    break;

                case RecoveryStrategy.CreateEmptyPlaylist:
                    recoveryResult = AttemptCreateEmptyPlaylistRecovery(errorResult);
                    break;

                case RecoveryStrategy.NoRecovery:
                    recoveryResult.IsSuccessful = false;
                    recoveryResult.RecoveryMessage = "No recovery strategy available for this error type";
                    break;

                default:
                    recoveryResult.IsSuccessful = false;
                    recoveryResult.RecoveryMessage = "Unknown recovery strategy";
                    break;
            }

            _logger.LogInformation("Recovery attempt for playlist '{PlaylistName}' completed: {Success}",
                errorResult.PlaylistName, recoveryResult.IsSuccessful);

            return recoveryResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Recovery attempt failed for playlist '{PlaylistName}': {Message}",
                errorResult.PlaylistName, ex.Message);

            recoveryResult.IsSuccessful = false;
            recoveryResult.RecoveryMessage = $"Recovery attempt failed: {ex.Message}";
            return recoveryResult;
        }
    }

    /// <summary>
    /// Classifies an exception into a specific error type.
    /// </summary>
    /// <param name="exception">The exception to classify.</param>
    /// <returns>The classified error type.</returns>
    private static PlaylistErrorType ClassifyError(Exception exception)
    {
        return exception switch
        {
            ArgumentNullException => PlaylistErrorType.InvalidInput,
            ArgumentException => PlaylistErrorType.InvalidInput,
            UnauthorizedAccessException => PlaylistErrorType.PermissionDenied,
            TimeoutException => PlaylistErrorType.Timeout,
            InvalidOperationException => PlaylistErrorType.InvalidState,
            NotSupportedException => PlaylistErrorType.UnsupportedOperation,
            _ => PlaylistErrorType.Unknown
        };
    }

    /// <summary>
    /// Logs an error with appropriate level and context.
    /// </summary>
    /// <param name="errorResult">The error result to log.</param>
    private void LogError(PlaylistErrorResult errorResult)
    {
        var logLevel = errorResult.Severity switch
        {
            ErrorSeverity.Critical => LogLevel.Error,
            ErrorSeverity.High => LogLevel.Warning,
            ErrorSeverity.Medium => LogLevel.Warning,
            ErrorSeverity.Low => LogLevel.Information,
            _ => LogLevel.Debug
        };

        _logger.Log(logLevel, errorResult.OriginalException,
            "Playlist operation failed for '{PlaylistName}': {ErrorType} - {Message}",
            errorResult.PlaylistName, errorResult.ErrorType, errorResult.OriginalException.Message);
    }

    /// <summary>
    /// Determines the appropriate recovery strategy for an error.
    /// </summary>
    /// <param name="errorResult">The error result to analyze.</param>
    /// <returns>The recommended recovery strategy.</returns>
    private static RecoveryStrategy DetermineRecoveryStrategy(PlaylistErrorResult errorResult)
    {
        return errorResult.ErrorType switch
        {
            PlaylistErrorType.Timeout => RecoveryStrategy.RetryWithDelay,
            PlaylistErrorType.InvalidInput => RecoveryStrategy.SkipInvalidItems,
            PlaylistErrorType.ItemNotFound => RecoveryStrategy.SkipInvalidItems,
            PlaylistErrorType.PermissionDenied => RecoveryStrategy.NoRecovery,
            PlaylistErrorType.InvalidState => RecoveryStrategy.RetryWithDelay,
            PlaylistErrorType.UnsupportedOperation => RecoveryStrategy.NoRecovery,
            _ => RecoveryStrategy.CreateEmptyPlaylist
        };
    }

    /// <summary>
    /// Generates a user-friendly error message.
    /// </summary>
    /// <param name="errorResult">The error result to generate a message for.</param>
    /// <returns>A user-friendly error message.</returns>
    private static string GenerateUserFriendlyMessage(PlaylistErrorResult errorResult)
    {
        return errorResult.ErrorType switch
        {
            PlaylistErrorType.InvalidInput => $"Invalid data provided for playlist '{errorResult.PlaylistName}'. Please check the configuration.",
            PlaylistErrorType.PermissionDenied => $"Permission denied when creating playlist '{errorResult.PlaylistName}'. Check user permissions.",
            PlaylistErrorType.Timeout => $"Timeout occurred while creating playlist '{errorResult.PlaylistName}'. The operation took too long.",
            PlaylistErrorType.ItemNotFound => $"Some items for playlist '{errorResult.PlaylistName}' were not found in the library.",
            PlaylistErrorType.InvalidState => $"System is in an invalid state for creating playlist '{errorResult.PlaylistName}'.",
            PlaylistErrorType.UnsupportedOperation => $"The requested operation for playlist '{errorResult.PlaylistName}' is not supported.",
            _ => $"An unexpected error occurred while creating playlist '{errorResult.PlaylistName}'."
        };
    }

    /// <summary>
    /// Determines if processing should continue after an error.
    /// </summary>
    /// <param name="errorResult">The error result to evaluate.</param>
    /// <returns>True if processing should continue, false otherwise.</returns>
    private static bool ShouldContinueProcessing(PlaylistErrorResult errorResult)
    {
        // Continue processing unless it's a critical system error
        return errorResult.Severity != ErrorSeverity.Critical ||
               errorResult.ErrorType != PlaylistErrorType.SystemFailure;
    }

    /// <summary>
    /// Generates recommendations for batch error handling.
    /// </summary>
    /// <param name="batchErrors">The batch errors to analyze.</param>
    /// <returns>A list of recommendations.</returns>
    private static List<string> GenerateBatchRecommendations(List<PlaylistErrorResult> batchErrors)
    {
        var recommendations = new List<string>();

        var permissionErrors = batchErrors.Count(e => e.ErrorType == PlaylistErrorType.PermissionDenied);
        if (permissionErrors > 0)
        {
            recommendations.Add($"Check user permissions - {permissionErrors} playlist(s) failed due to permission issues");
        }

        var timeoutErrors = batchErrors.Count(e => e.ErrorType == PlaylistErrorType.Timeout);
        if (timeoutErrors > 0)
        {
            recommendations.Add($"Consider reducing batch size - {timeoutErrors} playlist(s) timed out");
        }

        var itemNotFoundErrors = batchErrors.Count(e => e.ErrorType == PlaylistErrorType.ItemNotFound);
        if (itemNotFoundErrors > 0)
        {
            recommendations.Add($"Verify library content - {itemNotFoundErrors} playlist(s) had missing items");
        }

        if (batchErrors.Count > batchErrors.Count * 0.5) // More than 50% failed
        {
            recommendations.Add("High failure rate detected - consider reviewing configuration and system status");
        }

        return recommendations;
    }

    /// <summary>
    /// Logs a batch error summary.
    /// </summary>
    /// <param name="summary">The batch error summary to log.</param>
    private void LogBatchSummary(BatchErrorSummary summary)
    {
        if (summary.TotalErrors == 0)
        {
            _logger.LogInformation("Batch playlist operation completed successfully with no errors");
            return;
        }

        var logLevel = summary.OverallSeverity switch
        {
            ErrorSeverity.Critical => LogLevel.Error,
            ErrorSeverity.High => LogLevel.Warning,
            _ => LogLevel.Information
        };

        _logger.Log(logLevel,
            "Batch playlist operation completed with {TotalErrors} errors. Critical: {CriticalErrors}, Recoverable: {RecoverableErrors}",
            summary.TotalErrors, summary.CriticalErrors, summary.RecoverableErrors);

        foreach (var recommendation in summary.BatchRecommendations)
        {
            _logger.LogInformation("Recommendation: {Recommendation}", recommendation);
        }
    }

    /// <summary>
    /// Attempts retry recovery strategy.
    /// </summary>
    /// <param name="errorResult">The error result to recover from.</param>
    /// <returns>The recovery result.</returns>
    private PlaylistRecoveryResult AttemptRetryRecovery(PlaylistErrorResult errorResult)
    {
        // In a real implementation, this would retry the operation with a delay
        return new PlaylistRecoveryResult
        {
            OriginalError = errorResult,
            IsSuccessful = false, // Placeholder - would attempt actual retry
            RecoveryMessage = "Retry recovery not implemented in mock version",
            AttemptTimestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Attempts skip invalid items recovery strategy.
    /// </summary>
    /// <param name="errorResult">The error result to recover from.</param>
    /// <returns>The recovery result.</returns>
    private PlaylistRecoveryResult AttemptSkipInvalidItemsRecovery(PlaylistErrorResult errorResult)
    {
        // In a real implementation, this would filter out invalid items and retry
        return new PlaylistRecoveryResult
        {
            OriginalError = errorResult,
            IsSuccessful = false, // Placeholder - would attempt actual recovery
            RecoveryMessage = "Skip invalid items recovery not implemented in mock version",
            AttemptTimestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Attempts create empty playlist recovery strategy.
    /// </summary>
    /// <param name="errorResult">The error result to recover from.</param>
    /// <returns>The recovery result.</returns>
    private PlaylistRecoveryResult AttemptCreateEmptyPlaylistRecovery(PlaylistErrorResult errorResult)
    {
        // In a real implementation, this would create an empty playlist as a fallback
        return new PlaylistRecoveryResult
        {
            OriginalError = errorResult,
            IsSuccessful = false, // Placeholder - would attempt actual recovery
            RecoveryMessage = "Create empty playlist recovery not implemented in mock version",
            AttemptTimestamp = DateTime.UtcNow
        };
    }
}

/// <summary>
/// Represents the result of a playlist error handling operation.
/// </summary>
public class PlaylistErrorResult
{
    /// <summary>
    /// Gets or sets the playlist name that encountered the error.
    /// </summary>
    public string PlaylistName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of error that occurred.
    /// </summary>
    public PlaylistErrorType ErrorType { get; set; }

    /// <summary>
    /// Gets or sets the severity of the error.
    /// </summary>
    public ErrorSeverity Severity { get; set; }

    /// <summary>
    /// Gets or sets the original exception that caused the error.
    /// </summary>
    public Exception OriginalException { get; set; } = null!;

    /// <summary>
    /// Gets or sets the operation context when the error occurred.
    /// </summary>
    public PlaylistOperationContext Context { get; set; } = new();

    /// <summary>
    /// Gets or sets the recommended recovery strategy.
    /// </summary>
    public RecoveryStrategy RecoveryStrategy { get; set; }

    /// <summary>
    /// Gets or sets the user-friendly error message.
    /// </summary>
    public string UserFriendlyMessage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether processing should continue after this error.
    /// </summary>
    public bool ShouldContinueProcessing { get; set; } = true;

    /// <summary>
    /// Gets or sets the timestamp when the error occurred.
    /// </summary>
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Types of playlist operation errors.
/// </summary>
public enum PlaylistErrorType
{
    /// <summary>
    /// Invalid input data provided.
    /// </summary>
    InvalidInput,

    /// <summary>
    /// Permission denied for the operation.
    /// </summary>
    PermissionDenied,

    /// <summary>
    /// Operation timed out.
    /// </summary>
    Timeout,

    /// <summary>
    /// Required item not found.
    /// </summary>
    ItemNotFound,

    /// <summary>
    /// System is in an invalid state.
    /// </summary>
    InvalidState,

    /// <summary>
    /// Operation is not supported.
    /// </summary>
    UnsupportedOperation,

    /// <summary>
    /// Critical system failure.
    /// </summary>
    SystemFailure,

    /// <summary>
    /// Unknown error type.
    /// </summary>
    Unknown
}

/// <summary>
/// Error severity levels.
/// </summary>
public enum ErrorSeverity
{
    /// <summary>
    /// No error.
    /// </summary>
    None,

    /// <summary>
    /// Low severity - informational.
    /// </summary>
    Low,

    /// <summary>
    /// Medium severity - warning.
    /// </summary>
    Medium,

    /// <summary>
    /// High severity - error but recoverable.
    /// </summary>
    High,

    /// <summary>
    /// Critical severity - system failure.
    /// </summary>
    Critical
}

/// <summary>
/// Recovery strategies for playlist errors.
/// </summary>
public enum RecoveryStrategy
{
    /// <summary>
    /// No recovery possible.
    /// </summary>
    NoRecovery,

    /// <summary>
    /// Retry the operation with a delay.
    /// </summary>
    RetryWithDelay,

    /// <summary>
    /// Skip invalid items and retry.
    /// </summary>
    SkipInvalidItems,

    /// <summary>
    /// Create an empty playlist as fallback.
    /// </summary>
    CreateEmptyPlaylist
}

/// <summary>
/// Context information for playlist operations.
/// </summary>
public class PlaylistOperationContext
{
    /// <summary>
    /// Gets or sets the user ID performing the operation.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the number of items being processed.
    /// </summary>
    public int ItemCount { get; set; }

    /// <summary>
    /// Gets or sets the operation type being performed.
    /// </summary>
    public string OperationType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets additional context metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Summary of errors from batch playlist operations.
/// </summary>
public class BatchErrorSummary
{
    /// <summary>
    /// Gets or sets the total number of errors.
    /// </summary>
    public int TotalErrors { get; set; }

    /// <summary>
    /// Gets or sets the number of critical errors.
    /// </summary>
    public int CriticalErrors { get; set; }

    /// <summary>
    /// Gets or sets the number of recoverable errors.
    /// </summary>
    public int RecoverableErrors { get; set; }

    /// <summary>
    /// Gets or sets the overall severity of the batch.
    /// </summary>
    public ErrorSeverity OverallSeverity { get; set; }

    /// <summary>
    /// Gets the count of errors by type.
    /// </summary>
    public Dictionary<PlaylistErrorType, int> ErrorsByType { get; } = new();

    /// <summary>
    /// Gets the batch recommendations.
    /// </summary>
    public List<string> BatchRecommendations { get; set; } = new();

    /// <summary>
    /// Gets or sets the timestamp of the batch summary.
    /// </summary>
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Result of a playlist recovery attempt.
/// </summary>
public class PlaylistRecoveryResult
{
    /// <summary>
    /// Gets or sets the original error being recovered from.
    /// </summary>
    public PlaylistErrorResult OriginalError { get; set; } = null!;

    /// <summary>
    /// Gets or sets a value indicating whether the recovery was successful.
    /// </summary>
    public bool IsSuccessful { get; set; }

    /// <summary>
    /// Gets or sets the recovery message.
    /// </summary>
    public string RecoveryMessage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp of the recovery attempt.
    /// </summary>
    public DateTime AttemptTimestamp { get; set; }
}