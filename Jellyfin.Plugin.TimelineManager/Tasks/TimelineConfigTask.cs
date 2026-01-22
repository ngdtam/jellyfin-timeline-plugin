using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.TimelineManager.Services;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Playlists;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TimelineManager.Tasks;

/// <summary>
/// Main timeline processing task that orchestrates the complete workflow from configuration loading
/// to playlist creation. Implements IScheduledTask for integration with Jellyfin's task system.
/// </summary>
public class TimelineConfigTask : IScheduledTask
{
    private readonly ILogger<TimelineConfigTask> _logger;
    private readonly ILibraryManager _libraryManager;
    private readonly IPlaylistManager _playlistManager;
    private readonly ConfigurationService _configurationService;
    private readonly ContentLookupService _contentLookupService;
    private readonly ProviderMatchingService _providerMatchingService;
    private readonly MixedContentService _mixedContentService;
    private readonly PlaylistService _playlistService;
    private readonly MixedContentPlaylistService _mixedContentPlaylistService;
    private readonly PlaylistErrorHandler _errorHandler;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimelineConfigTask"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="libraryManager">The Jellyfin library manager.</param>
    /// <param name="playlistManager">The Jellyfin playlist manager.</param>
    public TimelineConfigTask(
        ILogger<TimelineConfigTask> logger,
        ILibraryManager libraryManager,
        IPlaylistManager playlistManager)
    {
        _logger = logger;
        _libraryManager = libraryManager;
        _playlistManager = playlistManager;

        // Initialize services with proper dependency injection
        _configurationService = new ConfigurationService(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ConfigurationService>.Instance);

        _contentLookupService = new ContentLookupService(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ContentLookupService>.Instance,
            _libraryManager);

        _providerMatchingService = new ProviderMatchingService(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ProviderMatchingService>.Instance,
            _contentLookupService);

        _mixedContentService = new MixedContentService(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<MixedContentService>.Instance,
            _providerMatchingService);

        _playlistService = new PlaylistService(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PlaylistService>.Instance,
            _playlistManager);

        _mixedContentPlaylistService = new MixedContentPlaylistService(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<MixedContentPlaylistService>.Instance,
            _playlistService,
            _mixedContentService);

        _errorHandler = new PlaylistErrorHandler(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PlaylistErrorHandler>.Instance);
    }

    /// <inheritdoc />
    public string Name => "Universal Timeline Manager";

    /// <inheritdoc />
    public string Description => "Creates and updates chronological playlists for multiple cinematic universes based on JSON configuration.";

    /// <inheritdoc />
    public string Category => "Timeline Management";

    /// <inheritdoc />
    public string Key => "TimelineManager";

    /// <inheritdoc />
    public bool IsHidden => false;

    /// <inheritdoc />
    public bool IsEnabled => true;

    /// <inheritdoc />
    public bool IsLogged => true;

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        // Return empty - manual execution only by default
        return Array.Empty<TaskTriggerInfo>();
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var executionResult = new TimelineExecutionResult
        {
            StartTime = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation("Starting Universal Timeline Manager execution at {StartTime}", executionResult.StartTime);
            _logger.LogDebug("Execution environment: Plugin={PluginName}, Task={TaskName}, Key={TaskKey}", 
                "TimelineManager", Name, Key);
            progress?.Report(0);

            // Validate system readiness before starting
            _logger.LogDebug("Validating system readiness and service availability");
            await ValidateSystemReadinessAsync(cancellationToken);

            // Step 1: Load Configuration (10% progress)
            _logger.LogInformation("Step 1: Loading timeline configuration");
            _logger.LogDebug("Configuration loading started from default path");
            var configuration = await LoadConfigurationAsync(cancellationToken);
            if (configuration == null)
            {
                executionResult.IsSuccess = false;
                executionResult.ErrorMessage = "Failed to load configuration";
                _logger.LogError("Execution terminated: Configuration loading failed");
                LogExecutionSummary(executionResult);
                return;
            }
            executionResult.UniverseCount = configuration.Universes.Count;
            _logger.LogInformation("Configuration loaded successfully: {UniverseCount} universes, {TotalItems} total items",
                configuration.Universes.Count, 
                configuration.Universes.Sum(u => u.Items.Count));
            progress?.Report(10);

            // Step 2: Build Content Lookup Tables (30% progress)
            _logger.LogInformation("Step 2: Building content lookup tables");
            _logger.LogDebug("Indexing library content for efficient Provider_ID matching");
            await BuildContentLookupTablesAsync(cancellationToken);
            progress?.Report(30);

            // Step 3: Process Each Universe (30-80% progress)
            _logger.LogInformation("Step 3: Processing {UniverseCount} universes", configuration.Universes.Count);
            _logger.LogDebug("Universe processing will match timeline items with library content");
            var universeResults = await ProcessUniversesAsync(configuration.Universes, progress, cancellationToken);
            executionResult.ProcessedUniverses = universeResults.Count;
            _logger.LogInformation("Universe processing completed: {ProcessedCount}/{TotalCount} universes processed",
                universeResults.Count, configuration.Universes.Count);
            progress?.Report(80);

            // Step 4: Create Playlists (80-95% progress)
            _logger.LogInformation("Step 4: Creating playlists for processed universes");
            _logger.LogDebug("Playlist creation will use Jellyfin IPlaylistManager service");
            var playlistResults = await CreatePlaylistsAsync(universeResults, cancellationToken);
            executionResult.CreatedPlaylists = playlistResults.Count(r => r.IsSuccess);
            executionResult.FailedPlaylists = playlistResults.Count(r => !r.IsSuccess);
            _logger.LogInformation("Playlist creation completed: {CreatedCount} created, {FailedCount} failed",
                executionResult.CreatedPlaylists, executionResult.FailedPlaylists);
            progress?.Report(95);

            // Step 5: Finalize and Report (95-100% progress)
            _logger.LogInformation("Step 5: Finalizing execution and generating summary");
            _logger.LogDebug("Calculating final statistics and performing cleanup");
            await FinalizeExecutionAsync(executionResult, playlistResults, universeResults, cancellationToken);
            progress?.Report(100);

            executionResult.IsSuccess = true;
            _logger.LogInformation("Universal Timeline Manager execution completed successfully in {Duration}",
                DateTime.UtcNow - executionResult.StartTime);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Universal Timeline Manager execution was cancelled by user request after {Duration}",
                DateTime.UtcNow - executionResult.StartTime);
            executionResult.IsSuccess = false;
            executionResult.ErrorMessage = "Execution was cancelled by user request";
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("Universal Timeline Manager execution timed out after {Duration}",
                DateTime.UtcNow - executionResult.StartTime);
            executionResult.IsSuccess = false;
            executionResult.ErrorMessage = "Execution timed out";
        }
        catch (TimeoutException ex)
        {
            _logger.LogError(ex, "Universal Timeline Manager execution failed due to timeout after {Duration}: {Message}",
                DateTime.UtcNow - executionResult.StartTime, ex.Message);
            executionResult.IsSuccess = false;
            executionResult.ErrorMessage = $"Operation timed out: {ex.Message}";
            
            _logger.LogInformation("Timeout Recovery Guidance:");
            _logger.LogInformation("- Check system performance and available resources");
            _logger.LogInformation("- Consider processing smaller batches of universes");
            _logger.LogInformation("- Verify network connectivity to Jellyfin services");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("service", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError(ex, "Universal Timeline Manager execution failed due to service unavailability after {Duration}: {Message}",
                DateTime.UtcNow - executionResult.StartTime, ex.Message);
            executionResult.IsSuccess = false;
            executionResult.ErrorMessage = $"Jellyfin service unavailable: {ex.Message}";
            
            _logger.LogInformation("Service Recovery Guidance:");
            _logger.LogInformation("- Verify Jellyfin server is running and accessible");
            _logger.LogInformation("- Check plugin registration and service dependencies");
            _logger.LogInformation("- Restart Jellyfin server if services appear corrupted");
            _logger.LogInformation("- Verify plugin permissions and user context");
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Universal Timeline Manager execution failed due to access denied after {Duration}: {Message}",
                DateTime.UtcNow - executionResult.StartTime, ex.Message);
            executionResult.IsSuccess = false;
            executionResult.ErrorMessage = $"Access denied: {ex.Message}";
            
            _logger.LogInformation("Permission Recovery Guidance:");
            _logger.LogInformation("- Verify plugin permissions in Jellyfin admin settings");
            _logger.LogInformation("- Check user context and access rights");
            _logger.LogInformation("- Ensure plugin is running with appropriate privileges");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Universal Timeline Manager execution failed after {Duration}: {Message}",
                DateTime.UtcNow - executionResult.StartTime, ex.Message);
            _logger.LogDebug("Exception details: Type={ExceptionType}, Source={Source}, StackTrace={StackTrace}",
                ex.GetType().Name, ex.Source, ex.StackTrace);
            executionResult.IsSuccess = false;
            executionResult.ErrorMessage = ex.Message;
            
            // Provide general recovery guidance
            LogSystemIntegrationErrorGuidance(ex, "timeline manager execution");
        }
        finally
        {
            executionResult.EndTime = DateTime.UtcNow;
            executionResult.Duration = executionResult.EndTime - executionResult.StartTime;
            _logger.LogDebug("Execution cleanup completed at {EndTime}", executionResult.EndTime);
            LogExecutionSummary(executionResult);
        }
    }

    /// <summary>
    /// Loads and validates the timeline configuration.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The loaded configuration or null if loading failed.</returns>
    private async Task<Models.TimelineConfiguration?> LoadConfigurationAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            _logger.LogDebug("Attempting to load configuration from ConfigurationService");
            var configuration = await _configurationService.LoadConfigurationAsync();
            if (configuration == null)
            {
                _logger.LogError("Configuration loading returned null - check configuration file existence and format");
                _logger.LogInformation("Configuration Loading Troubleshooting:");
                _logger.LogInformation("1. Verify the configuration file exists at the expected path");
                _logger.LogInformation("2. Check that the JSON syntax is valid");
                _logger.LogInformation("3. Ensure all required fields are present");
                _logger.LogInformation("4. Verify file permissions allow read access");
                _logger.LogInformation("5. Check the logs above for specific error details");
                return null;
            }

            _logger.LogInformation("Successfully loaded configuration with {UniverseCount} universes",
                configuration.Universes.Count);

            // Log detailed configuration information for troubleshooting
            foreach (var universe in configuration.Universes)
            {
                _logger.LogDebug("Universe '{UniverseName}' (Key: {UniverseKey}): {ItemCount} items",
                    universe.Name, universe.Key, universe.Items.Count);
                
                var providerBreakdown = universe.Items
                    .GroupBy(item => item.ProviderName.ToLowerInvariant())
                    .ToDictionary(g => g.Key, g => g.Count());
                
                foreach (var (provider, count) in providerBreakdown)
                {
                    _logger.LogDebug("  - {Provider}: {Count} items", provider, count);
                }
            }

            return configuration;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Configuration loading was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load timeline configuration: {Message}", ex.Message);
            _logger.LogDebug("Configuration loading error details: Type={ExceptionType}, HResult={HResult}",
                ex.GetType().Name, ex.HResult);
            
            // Provide recovery suggestions based on exception type
            LogConfigurationRecoveryGuidance(ex);
            return null;
        }
    }

    /// <summary>
    /// Logs recovery guidance based on the type of configuration error.
    /// </summary>
    /// <param name="exception">The exception that occurred during configuration loading.</param>
    private void LogConfigurationRecoveryGuidance(Exception exception)
    {
        _logger.LogInformation("Configuration Recovery Guidance:");
        
        switch (exception)
        {
            case FileNotFoundException:
                _logger.LogInformation("- Create the configuration file at the expected location");
                _logger.LogInformation("- Use the sample configuration format provided in the logs");
                break;
                
            case UnauthorizedAccessException:
                _logger.LogInformation("- Check file permissions for the configuration file");
                _logger.LogInformation("- Ensure the Jellyfin service has read access to the file");
                break;
                
            case DirectoryNotFoundException:
                _logger.LogInformation("- Create the configuration directory");
                _logger.LogInformation("- Ensure the full path to the configuration file exists");
                break;
                
            case JsonException:
                _logger.LogInformation("- Validate JSON syntax using an online JSON validator");
                _logger.LogInformation("- Check for missing commas, quotes, or brackets");
                _logger.LogInformation("- Ensure proper nesting of objects and arrays");
                break;
                
            default:
                _logger.LogInformation("- Check the error details above for specific guidance");
                _logger.LogInformation("- Verify the configuration file format matches the expected schema");
                _logger.LogInformation("- Consider recreating the configuration file from the sample");
                break;
        }
    }

    /// <summary>
    /// Validates that required Jellyfin services are available and accessible.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the validation operation.</returns>
    private async Task ValidateJellyfinServicesAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            _logger.LogDebug("Validating Jellyfin service availability");
            
            // Validate library manager
            if (_libraryManager == null)
            {
                throw new InvalidOperationException("Library manager service is not available");
            }
            
            // Test library manager accessibility with timeout
            await Task.Run(() =>
            {
                try
                {
                    // Attempt to access library manager - this will throw if service is unavailable
                    var testAccess = _libraryManager.GetItemsResult(new MediaBrowser.Controller.Entities.InternalItemsQuery
                    {
                        Limit = 1,
                        IncludeItemTypes = new[] { Jellyfin.Data.Enums.BaseItemKind.Movie }
                    });
                    
                    _logger.LogDebug("Library manager validation successful");
                }
                catch (Exception ex)
                {
                    _logger.LogError("Library manager validation failed: {Message}", ex.Message);
                    throw new InvalidOperationException("Library manager service validation failed", ex);
                }
            }, cancellationToken);
            
            // Validate playlist manager
            if (_playlistManager == null)
            {
                throw new InvalidOperationException("Playlist manager service is not available");
            }
            
            // Test playlist manager accessibility
            await Task.Run(() =>
            {
                try
                {
                    // Attempt to access playlist manager - this will throw if service is unavailable
                    var testAccess = _playlistManager.GetPlaylists(Guid.Empty);
                    _logger.LogDebug("Playlist manager validation successful");
                }
                catch (Exception ex)
                {
                    _logger.LogError("Playlist manager validation failed: {Message}", ex.Message);
                    throw new InvalidOperationException("Playlist manager service validation failed", ex);
                }
            }, cancellationToken);
            
            _logger.LogDebug("All Jellyfin services validated successfully");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Service validation was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Jellyfin service validation failed: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Validates overall system readiness before starting timeline processing.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the validation operation.</returns>
    private async Task ValidateSystemReadinessAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            _logger.LogDebug("Starting system readiness validation");
            
            // Check system resources
            var availableMemory = GC.GetTotalMemory(false);
            _logger.LogDebug("Available memory: {MemoryMB} MB", availableMemory / (1024 * 1024));
            
            if (availableMemory < 50 * 1024 * 1024) // Less than 50MB
            {
                _logger.LogWarning("Low memory detected ({MemoryMB} MB). Timeline processing may be slower or fail.",
                    availableMemory / (1024 * 1024));
            }
            
            // Validate Jellyfin services with timeout
            using var serviceValidationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            serviceValidationCts.CancelAfter(TimeSpan.FromSeconds(30)); // 30-second timeout for service validation
            
            await ValidateJellyfinServicesAsync(serviceValidationCts.Token);
            
            _logger.LogInformation("System readiness validation completed successfully");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("System readiness validation was cancelled by user request");
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("System readiness validation timed out after 30 seconds");
            throw new TimeoutException("System readiness validation timed out");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "System readiness validation failed: {Message}", ex.Message);
            _logger.LogInformation("System Readiness Recovery Guidance:");
            _logger.LogInformation("- Verify Jellyfin server is fully started and initialized");
            _logger.LogInformation("- Check system resources (memory, CPU, disk space)");
            _logger.LogInformation("- Ensure all required services are running");
            _logger.LogInformation("- Wait for system initialization to complete before retrying");
            throw;
        }
    }

    /// <summary>
    /// Logs system integration error guidance based on the exception type and operation context.
    /// </summary>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="operationContext">The context of the operation that failed.</param>
    private void LogSystemIntegrationErrorGuidance(Exception exception, string operationContext)
    {
        _logger.LogInformation("System Integration Error Guidance for {OperationContext}:", operationContext);
        
        switch (exception)
        {
            case TimeoutException:
                _logger.LogInformation("- Operation timed out - check system performance and load");
                _logger.LogInformation("- Consider increasing timeout values if processing large datasets");
                _logger.LogInformation("- Verify network connectivity to Jellyfin services");
                break;
                
            case InvalidOperationException when exception.Message.Contains("service", StringComparison.OrdinalIgnoreCase):
                _logger.LogInformation("- Jellyfin service is unavailable or not properly initialized");
                _logger.LogInformation("- Verify Jellyfin server is running and accessible");
                _logger.LogInformation("- Check plugin registration and service dependencies");
                _logger.LogInformation("- Restart Jellyfin server if services appear corrupted");
                break;
                
            case UnauthorizedAccessException:
                _logger.LogInformation("- Plugin lacks necessary permissions for the operation");
                _logger.LogInformation("- Verify plugin permissions in Jellyfin admin settings");
                _logger.LogInformation("- Check user context and access rights");
                _logger.LogInformation("- Ensure plugin is running with appropriate privileges");
                break;
                
            case System.Net.Sockets.SocketException:
                _logger.LogInformation("- Network connectivity issues detected");
                _logger.LogInformation("- Verify network connection to Jellyfin server");
                _logger.LogInformation("- Check firewall settings and port accessibility");
                break;
                
            case OutOfMemoryException:
                _logger.LogInformation("- Insufficient memory for the operation");
                _logger.LogInformation("- Consider processing smaller batches of data");
                _logger.LogInformation("- Verify system memory availability");
                _logger.LogInformation("- Check for memory leaks in long-running operations");
                break;
                
            default:
                _logger.LogInformation("- Unexpected system error occurred");
                _logger.LogInformation("- Check Jellyfin server logs for additional details");
                _logger.LogInformation("- Verify system resources and service health");
                _logger.LogInformation("- Consider restarting the plugin or Jellyfin server");
                break;
        }
        
        _logger.LogInformation("- Monitor system resources during operation");
        _logger.LogInformation("- Enable debug logging for more detailed error information");
    }

    /// <summary>
    /// Builds the content lookup tables for efficient Provider_ID matching.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    private async Task BuildContentLookupTablesAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            _logger.LogDebug("Starting content lookup table construction");
            var startTime = DateTime.UtcNow;
            
            // Add timeout handling for library operations
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromMinutes(5)); // 5-minute timeout for library indexing
            
            // Verify library manager availability before proceeding
            await ValidateJellyfinServicesAsync(timeoutCts.Token);
            
            await Task.Run(() => _contentLookupService.BuildLookupTables(), timeoutCts.Token);
            
            var buildDuration = DateTime.UtcNow - startTime;
            var stats = _contentLookupService.GetLookupStatistics();
            
            _logger.LogInformation("Content lookup tables built successfully in {Duration}: {TotalItems} items indexed",
                buildDuration, stats["TotalItemsIndexed"]);
            
            // Log detailed statistics for troubleshooting
            _logger.LogDebug("Lookup table statistics:");
            foreach (var (key, value) in stats)
            {
                _logger.LogDebug("  - {StatName}: {StatValue}", key, value);
            }
            
            if (stats.TryGetValue("TotalItemsIndexed", out var totalItems) && 
                totalItems is int itemCount && itemCount == 0)
            {
                _logger.LogWarning("No items were indexed - library may be empty or inaccessible");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Content lookup table construction was cancelled by user request");
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("Content lookup table construction timed out after 5 minutes. " +
                "This may indicate a very large library or system performance issues.");
            throw new TimeoutException("Library indexing operation timed out");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("service", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError(ex, "Jellyfin library service is unavailable: {Message}", ex.Message);
            _logger.LogInformation("Service Recovery Guidance:");
            _logger.LogInformation("- Verify Jellyfin server is running and accessible");
            _logger.LogInformation("- Check that the library service is properly initialized");
            _logger.LogInformation("- Ensure the plugin has proper permissions to access library services");
            throw new InvalidOperationException("Jellyfin library service unavailable", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied to library services: {Message}", ex.Message);
            _logger.LogInformation("Permission Recovery Guidance:");
            _logger.LogInformation("- Verify plugin permissions in Jellyfin admin settings");
            _logger.LogInformation("- Check that the plugin is properly registered and enabled");
            _logger.LogInformation("- Ensure the user context has library access rights");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build content lookup tables: {Message}", ex.Message);
            _logger.LogDebug("Lookup table build error: This may indicate library access issues or insufficient permissions");
            
            // Provide specific guidance based on exception type
            LogSystemIntegrationErrorGuidance(ex, "library indexing");
            throw;
        }
    }

    /// <summary>
    /// Processes all universes and matches their timeline items with library content.
    /// </summary>
    /// <param name="universes">The universes to process.</param>
    /// <param name="progress">The progress reporter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of mixed content results.</returns>
    private async Task<List<MixedContentResult>> ProcessUniversesAsync(
        List<Models.Universe> universes,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var results = new List<MixedContentResult>();
        var progressStep = 50.0 / Math.Max(universes.Count, 1); // 50% of total progress for this step
        var currentProgress = 30.0; // Starting at 30%

        for (int i = 0; i < universes.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var universe = universes[i];
            
            try
            {
                _logger.LogDebug("Processing universe '{UniverseName}' ({Index}/{Total})",
                    universe.Name, i + 1, universes.Count);

                var result = await Task.Run(() => 
                    _mixedContentService.ProcessMixedContentUniverse(universe), cancellationToken);

                results.Add(result);

                // Log detailed results including missing items
                _logger.LogInformation("Universe '{UniverseName}' processed: {MatchedCount}/{TotalCount} items matched",
                    universe.Name, result.MatchingResult.MatchedItems.Count, universe.Items.Count);

                // Log missing items with warnings if any
                if (result.MatchingResult.MissingItems.Count > 0)
                {
                    _logger.LogWarning("Universe '{UniverseName}' has {MissingCount} missing items that were not found in the library: {MissingItems}",
                        universe.Name, 
                        result.MatchingResult.MissingItems.Count, 
                        string.Join(", ", result.MatchingResult.MissingItems));
                    
                    // Log each missing item individually for better troubleshooting
                    foreach (var missingItem in result.MatchingResult.MissingTimelineItems)
                    {
                        _logger.LogWarning("Missing item: ProviderId='{ProviderId}', Provider='{Provider}', Type='{Type}' (Key: {ProviderKey})",
                            missingItem.ProviderId, 
                            missingItem.ProviderName, 
                            missingItem.Type,
                            missingItem.ProviderKey);
                    }
                }
                else
                {
                    _logger.LogInformation("Universe '{UniverseName}' - All {TotalCount} items were successfully matched",
                        universe.Name, universe.Items.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process universe '{UniverseName}': {Message}",
                    universe.Name, ex.Message);

                // Create error result to maintain processing continuity
                var errorResult = new MixedContentResult
                {
                    UniverseKey = universe.Key,
                    UniverseName = universe.Name,
                    ValidationResult = new MixedContentValidationResult
                    {
                        IsValid = false,
                        Errors = { ex.Message }
                    }
                };
                results.Add(errorResult);
            }

            currentProgress += progressStep;
            progress?.Report(currentProgress);
        }

        return results;
    }

    /// <summary>
    /// Creates playlists from the processed universe results.
    /// </summary>
    /// <param name="universeResults">The processed universe results.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of playlist operation results.</returns>
    private async Task<List<MixedContentPlaylistResult>> CreatePlaylistsAsync(
        List<MixedContentResult> universeResults,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            _logger.LogDebug("Starting playlist creation for {UniverseCount} universes", universeResults.Count);
            
            // Add timeout handling for playlist operations
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromMinutes(10)); // 10-minute timeout for playlist operations
            
            // Verify playlist manager availability before proceeding
            await ValidateJellyfinServicesAsync(timeoutCts.Token);
            
            // Use a default user ID for playlist creation
            // In a real implementation, this would come from the task context or configuration
            var userId = Guid.Empty; // Placeholder - would be actual user ID
            _logger.LogDebug("Using user ID {UserId} for playlist creation", userId);

            var playlistResults = await _mixedContentPlaylistService.CreateMixedContentPlaylistsAsync(
                universeResults, userId);

            // Log detailed playlist creation results
            var successfulPlaylists = playlistResults.Count(r => r.IsSuccess);
            var failedPlaylists = playlistResults.Count(r => !r.IsSuccess);
            
            _logger.LogInformation("Playlist creation summary: {SuccessCount} successful, {FailedCount} failed",
                successfulPlaylists, failedPlaylists);

            foreach (var result in playlistResults)
            {
                if (result.IsSuccess)
                {
                    _logger.LogDebug("Playlist '{PlaylistName}' created successfully: {ItemCount} items ({MovieCount} movies, {EpisodeCount} episodes)",
                        result.UniverseName, result.TotalItemCount, result.MovieCount, result.EpisodeCount);
                }
                else
                {
                    _logger.LogWarning("Playlist '{PlaylistName}' creation failed: {ErrorMessage}",
                        result.UniverseName, result.ErrorMessage);
                }
            }

            return playlistResults;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Playlist creation was cancelled by user request");
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("Playlist creation timed out after 10 minutes. " +
                "This may indicate playlist service issues or very large playlists.");
            
            // Return partial results for universes that may have been processed
            return universeResults.Select(ur => new MixedContentPlaylistResult
            {
                UniverseName = ur.UniverseName,
                IsSuccess = false,
                ErrorMessage = "Operation timed out",
                ContentTypeAnalysis = ur.ContentTypeAnalysis
            }).ToList();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("playlist", StringComparison.OrdinalIgnoreCase) ||
                                                   ex.Message.Contains("service", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError(ex, "Jellyfin playlist service is unavailable: {Message}", ex.Message);
            _logger.LogInformation("Playlist Service Recovery Guidance:");
            _logger.LogInformation("- Verify Jellyfin server is running and accessible");
            _logger.LogInformation("- Check that the playlist service is properly initialized");
            _logger.LogInformation("- Ensure the plugin has proper permissions to create playlists");
            _logger.LogInformation("- Verify user permissions for playlist creation");
            
            // Return error results for all universes to maintain consistency
            return universeResults.Select(ur => new MixedContentPlaylistResult
            {
                UniverseName = ur.UniverseName,
                IsSuccess = false,
                ErrorMessage = "Playlist service unavailable: " + ex.Message,
                ContentTypeAnalysis = ur.ContentTypeAnalysis
            }).ToList();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied to playlist services: {Message}", ex.Message);
            _logger.LogInformation("Playlist Permission Recovery Guidance:");
            _logger.LogInformation("- Verify plugin permissions in Jellyfin admin settings");
            _logger.LogInformation("- Check that the user has playlist creation rights");
            _logger.LogInformation("- Ensure the plugin is running with appropriate user context");
            
            // Return error results for all universes
            return universeResults.Select(ur => new MixedContentPlaylistResult
            {
                UniverseName = ur.UniverseName,
                IsSuccess = false,
                ErrorMessage = "Access denied to playlist services",
                ContentTypeAnalysis = ur.ContentTypeAnalysis
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create playlists: {Message}", ex.Message);
            _logger.LogDebug("Playlist creation failure may indicate Jellyfin service issues or permission problems");
            
            // Provide specific guidance based on exception type
            LogSystemIntegrationErrorGuidance(ex, "playlist creation");
            
            // Return error results for all universes to maintain consistency
            return universeResults.Select(ur => new MixedContentPlaylistResult
            {
                UniverseName = ur.UniverseName,
                IsSuccess = false,
                ErrorMessage = ex.Message,
                ContentTypeAnalysis = ur.ContentTypeAnalysis
            }).ToList();
        }
    }

    /// <summary>
    /// Finalizes the execution and performs cleanup tasks.
    /// </summary>
    /// <param name="executionResult">The execution result to finalize.</param>
    /// <param name="playlistResults">The playlist results.</param>
    /// <param name="universeResults">The universe processing results.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    private async Task FinalizeExecutionAsync(
        TimelineExecutionResult executionResult,
        List<MixedContentPlaylistResult> playlistResults,
        List<MixedContentResult> universeResults,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            _logger.LogDebug("Starting execution finalization and statistics calculation");

            // Handle any playlist errors
            var errors = playlistResults
                .Where(r => !r.IsSuccess)
                .Select(r => new PlaylistErrorResult
                {
                    PlaylistName = r.UniverseName,
                    ErrorType = PlaylistErrorType.Unknown,
                    OriginalException = new Exception(r.ErrorMessage ?? "Unknown error"),
                    UserFriendlyMessage = r.ErrorMessage ?? "Unknown error occurred"
                })
                .ToList();

            if (errors.Count > 0)
            {
                _logger.LogWarning("Processing {ErrorCount} playlist errors for batch handling", errors.Count);
                var batchSummary = _errorHandler.HandleBatchPlaylistErrors(errors);
                executionResult.ErrorSummary = batchSummary;
                
                _logger.LogDebug("Error batch summary: {TotalErrors} total, {CriticalErrors} critical, {RecoverableErrors} recoverable",
                    batchSummary.TotalErrors, batchSummary.CriticalErrors, batchSummary.RecoverableErrors);
            }

            // Calculate final statistics
            executionResult.TotalItemsProcessed = playlistResults.Sum(r => r.TotalItemCount);
            executionResult.TotalMovies = playlistResults.Sum(r => r.MovieCount);
            executionResult.TotalEpisodes = playlistResults.Sum(r => r.EpisodeCount);

            // Calculate missing items statistics from universe results
            var totalMissingItems = universeResults
                .Where(r => r.MatchingResult != null)
                .Sum(r => r.MatchingResult.MissingItems.Count);

            executionResult.TotalMissingItems = totalMissingItems;

            _logger.LogInformation("Final statistics calculated: {TotalItems} items processed ({Movies} movies, {Episodes} episodes), {MissingItems} missing",
                executionResult.TotalItemsProcessed, executionResult.TotalMovies, executionResult.TotalEpisodes, totalMissingItems);

            if (totalMissingItems > 0)
            {
                _logger.LogWarning("Execution completed with {MissingItemCount} missing items across all universes. " +
                    "These items were not found in the library and were skipped during playlist creation. " +
                    "Check library content and Provider_ID accuracy.",
                    totalMissingItems);
                
                // Log missing items by universe for troubleshooting
                foreach (var universeResult in universeResults.Where(r => r.MatchingResult?.MissingItems.Count > 0))
                {
                    _logger.LogDebug("Universe '{UniverseName}' missing items: {MissingItems}",
                        universeResult.UniverseName,
                        string.Join(", ", universeResult.MatchingResult.MissingItems));
                }
            }

            await Task.CompletedTask; // Placeholder for any async cleanup
            _logger.LogDebug("Execution finalization completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during execution finalization: {Message}", ex.Message);
            _logger.LogDebug("Finalization error details: This may affect statistics accuracy but not core functionality");
        }
    }

    /// <summary>
    /// Logs a comprehensive execution summary.
    /// </summary>
    /// <param name="result">The execution result to log.</param>
    private void LogExecutionSummary(TimelineExecutionResult result)
    {
        var logLevel = result.IsSuccess ? LogLevel.Information : LogLevel.Error;
        
        _logger.Log(logLevel, 
            "Timeline Manager Execution Summary:\n" +
            "  Status: {Status}\n" +
            "  Duration: {Duration}\n" +
            "  Universes: {UniverseCount} configured, {ProcessedCount} processed\n" +
            "  Playlists: {CreatedCount} created, {FailedCount} failed\n" +
            "  Content: {TotalItems} items ({Movies} movies, {Episodes} episodes)\n" +
            "  Missing Items: {MissingItems} items not found in library\n" +
            "  Error: {ErrorMessage}",
            result.IsSuccess ? "SUCCESS" : "FAILED",
            result.Duration,
            result.UniverseCount,
            result.ProcessedUniverses,
            result.CreatedPlaylists,
            result.FailedPlaylists,
            result.TotalItemsProcessed,
            result.TotalMovies,
            result.TotalEpisodes,
            result.TotalMissingItems,
            result.ErrorMessage ?? "None");

        if (result.ErrorSummary != null && result.ErrorSummary.TotalErrors > 0)
        {
            _logger.LogWarning("Error Summary: {TotalErrors} errors occurred during execution. " +
                "Check individual error logs for detailed troubleshooting information.",
                result.ErrorSummary.TotalErrors);
        }

        // Log performance metrics for troubleshooting
        if (result.Duration.TotalSeconds > 30)
        {
            _logger.LogInformation("Performance note: Execution took {Duration} - consider library size and system performance",
                result.Duration);
        }

        // Log recommendations based on results
        if (result.TotalMissingItems > 0 && result.TotalItemsProcessed > 0)
        {
            var missingPercentage = (double)result.TotalMissingItems / (result.TotalItemsProcessed + result.TotalMissingItems) * 100;
            if (missingPercentage > 20)
            {
                _logger.LogWarning("High missing item rate ({MissingPercentage:F1}%) - verify library content and Provider_ID accuracy",
                    missingPercentage);
            }
        }

        if (result.FailedPlaylists > 0)
        {
            _logger.LogWarning("Some playlists failed to create - check Jellyfin permissions and playlist service availability");
        }
    }
}

/// <summary>
/// Represents the result of a timeline execution.
/// </summary>
public class TimelineExecutionResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the execution was successful.
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Gets or sets the start time of execution.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Gets or sets the end time of execution.
    /// </summary>
    public DateTime EndTime { get; set; }

    /// <summary>
    /// Gets or sets the duration of execution.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets or sets the number of configured universes.
    /// </summary>
    public int UniverseCount { get; set; }

    /// <summary>
    /// Gets or sets the number of processed universes.
    /// </summary>
    public int ProcessedUniverses { get; set; }

    /// <summary>
    /// Gets or sets the number of successfully created playlists.
    /// </summary>
    public int CreatedPlaylists { get; set; }

    /// <summary>
    /// Gets or sets the number of failed playlist operations.
    /// </summary>
    public int FailedPlaylists { get; set; }

    /// <summary>
    /// Gets or sets the total number of items processed.
    /// </summary>
    public int TotalItemsProcessed { get; set; }

    /// <summary>
    /// Gets or sets the total number of movies processed.
    /// </summary>
    public int TotalMovies { get; set; }

    /// <summary>
    /// Gets or sets the total number of episodes processed.
    /// </summary>
    public int TotalEpisodes { get; set; }

    /// <summary>
    /// Gets or sets the total number of missing items that were not found in the library.
    /// </summary>
    public int TotalMissingItems { get; set; }

    /// <summary>
    /// Gets or sets the error message (if execution failed).
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the error summary for batch operations.
    /// </summary>
    public BatchErrorSummary? ErrorSummary { get; set; }
}