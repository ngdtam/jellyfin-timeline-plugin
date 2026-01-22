using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TimelineManager.Services;

/// <summary>
/// Extension service for creating playlists with mixed content types (movies and TV episodes).
/// Builds on top of PlaylistService to provide specialized mixed content handling.
/// </summary>
public class MixedContentPlaylistService
{
    private readonly ILogger<MixedContentPlaylistService> _logger;
    private readonly PlaylistService _playlistService;
    private readonly MixedContentService _mixedContentService;

    /// <summary>
    /// Initializes a new instance of the <see cref="MixedContentPlaylistService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="playlistService">The base playlist service.</param>
    /// <param name="mixedContentService">The mixed content service.</param>
    public MixedContentPlaylistService(
        ILogger<MixedContentPlaylistService> logger,
        PlaylistService playlistService,
        MixedContentService mixedContentService)
    {
        _logger = logger;
        _playlistService = playlistService;
        _mixedContentService = mixedContentService;
    }

    /// <summary>
    /// Creates playlists from mixed content universe results with enhanced mixed content support.
    /// </summary>
    /// <param name="mixedContentResults">The mixed content results to create playlists from.</param>
    /// <param name="userId">The user ID for playlist ownership.</param>
    /// <returns>A collection of mixed content playlist operation results.</returns>
    public async Task<List<MixedContentPlaylistResult>> CreateMixedContentPlaylistsAsync(
        List<MixedContentResult> mixedContentResults,
        Guid userId)
    {
        var results = new List<MixedContentPlaylistResult>();

        if (mixedContentResults == null || mixedContentResults.Count == 0)
        {
            _logger.LogWarning("No mixed content results provided for playlist creation");
            return results;
        }

        _logger.LogInformation("Creating mixed content playlists for {UniverseCount} universes", 
            mixedContentResults.Count);

        foreach (var mixedResult in mixedContentResults)
        {
            try
            {
                var playlistResult = await CreateSingleMixedContentPlaylistAsync(mixedResult, userId);
                results.Add(playlistResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create mixed content playlist for universe '{UniverseName}': {Message}",
                    mixedResult.UniverseName, ex.Message);

                var errorResult = new MixedContentPlaylistResult
                {
                    UniverseName = mixedResult.UniverseName,
                    IsSuccess = false,
                    ErrorMessage = ex.Message,
                    ContentTypeAnalysis = mixedResult.ContentTypeAnalysis
                };
                results.Add(errorResult);
            }
        }

        var successCount = results.Count(r => r.IsSuccess);
        _logger.LogInformation("Mixed content playlist creation completed: {SuccessCount}/{TotalCount} successful",
            successCount, results.Count);

        return results;
    }

    /// <summary>
    /// Creates a playlist that maintains chronological order across mixed content types.
    /// </summary>
    /// <param name="playlistName">The name of the playlist.</param>
    /// <param name="mixedItems">The mixed content items in chronological order.</param>
    /// <param name="userId">The user ID for playlist ownership.</param>
    /// <returns>A mixed content playlist operation result.</returns>
    public async Task<MixedContentPlaylistResult> CreateChronologicalMixedPlaylistAsync(
        string playlistName,
        List<MixedContentItem> mixedItems,
        Guid userId)
    {
        if (string.IsNullOrWhiteSpace(playlistName))
        {
            throw new ArgumentException("Playlist name cannot be null or empty", nameof(playlistName));
        }

        if (mixedItems == null)
        {
            throw new ArgumentNullException(nameof(mixedItems));
        }

        var result = new MixedContentPlaylistResult
        {
            UniverseName = playlistName
        };

        try
        {
            _logger.LogInformation("Creating chronological mixed content playlist '{PlaylistName}' with {ItemCount} items",
                playlistName, mixedItems.Count);

            // Analyze content type distribution
            var contentAnalysis = AnalyzeMixedContentDistribution(mixedItems);
            result.ContentTypeAnalysis = contentAnalysis;

            // Validate mixed content compatibility
            if (!ValidateMixedContentForPlaylist(mixedItems))
            {
                result.IsSuccess = false;
                result.ErrorMessage = "Mixed content validation failed";
                return result;
            }

            // Extract item IDs in chronological order
            var orderedItemIds = mixedItems.Select(item => item.ItemId).ToList();

            // Create playlist using base service
            var playlistResult = await _playlistService.CreateOrUpdatePlaylistAsync(
                playlistName, orderedItemIds, userId);

            // Map result to mixed content result
            result.IsSuccess = playlistResult.IsSuccess;
            result.PlaylistId = playlistResult.PlaylistId;
            result.OperationType = playlistResult.OperationType;
            result.TotalItemCount = playlistResult.FinalItemCount;
            result.ErrorMessage = playlistResult.ErrorMessage;

            // Add mixed content specific information
            result.MovieCount = mixedItems.Count(item => item.ContentType == MixedContentType.Movie);
            result.EpisodeCount = mixedItems.Count(item => item.ContentType == MixedContentType.Episode);
            result.ChronologicalOrderMaintained = true; // Input order is maintained

            _logger.LogInformation("Mixed content playlist '{PlaylistName}' created: {MovieCount} movies, {EpisodeCount} episodes",
                playlistName, result.MovieCount, result.EpisodeCount);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create chronological mixed playlist '{PlaylistName}': {Message}",
                playlistName, ex.Message);

            result.IsSuccess = false;
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    /// <summary>
    /// Validates playlist content type distribution and provides recommendations.
    /// </summary>
    /// <param name="mixedItems">The mixed content items to validate.</param>
    /// <returns>A validation result with recommendations.</returns>
    public MixedContentValidationResult ValidatePlaylistContentDistribution(List<MixedContentItem> mixedItems)
    {
        var result = new MixedContentValidationResult { IsValid = true };

        if (mixedItems == null || mixedItems.Count == 0)
        {
            result.IsValid = false;
            result.Errors.Add("No items provided for validation");
            return result;
        }

        var movieCount = mixedItems.Count(item => item.ContentType == MixedContentType.Movie);
        var episodeCount = mixedItems.Count(item => item.ContentType == MixedContentType.Episode);
        var otherCount = mixedItems.Count(item => item.ContentType == MixedContentType.Other);

        // Analyze distribution
        var totalItems = mixedItems.Count;
        var moviePercentage = (double)movieCount / totalItems * 100;
        var episodePercentage = (double)episodeCount / totalItems * 100;

        result.IsMixedContent = movieCount > 0 && episodeCount > 0;
        result.DetectedContentTypes = new List<string>();

        if (movieCount > 0) result.DetectedContentTypes.Add("movie");
        if (episodeCount > 0) result.DetectedContentTypes.Add("episode");
        if (otherCount > 0) result.DetectedContentTypes.Add("other");

        // Provide recommendations based on distribution
        if (moviePercentage > 80)
        {
            _logger.LogInformation("Playlist is primarily movies ({MoviePercentage:F1}%)", moviePercentage);
        }
        else if (episodePercentage > 80)
        {
            _logger.LogInformation("Playlist is primarily episodes ({EpisodePercentage:F1}%)", episodePercentage);
        }
        else if (result.IsMixedContent)
        {
            _logger.LogInformation("Playlist has balanced mixed content: {MoviePercentage:F1}% movies, {EpisodePercentage:F1}% episodes",
                moviePercentage, episodePercentage);
        }

        if (otherCount > 0)
        {
            result.Errors.Add($"Found {otherCount} items with unsupported content types");
            result.IsValid = false;
        }

        return result;
    }

    /// <summary>
    /// Creates a single mixed content playlist from a mixed content result.
    /// </summary>
    /// <param name="mixedResult">The mixed content result.</param>
    /// <param name="userId">The user ID for playlist ownership.</param>
    /// <returns>A mixed content playlist result.</returns>
    private async Task<MixedContentPlaylistResult> CreateSingleMixedContentPlaylistAsync(
        MixedContentResult mixedResult,
        Guid userId)
    {
        var result = new MixedContentPlaylistResult
        {
            UniverseName = mixedResult.UniverseName,
            ContentTypeAnalysis = mixedResult.ContentTypeAnalysis
        };

        if (!mixedResult.ValidationResult.IsValid)
        {
            result.IsSuccess = false;
            result.ErrorMessage = "Mixed content validation failed";
            return result;
        }

        if (mixedResult.MatchingResult.MatchedItems.Count == 0)
        {
            result.IsSuccess = false;
            result.ErrorMessage = "No matched items found";
            return result;
        }

        // Create playlist using the matched items in chronological order
        var playlistResult = await _playlistService.CreateOrUpdatePlaylistAsync(
            mixedResult.UniverseName,
            mixedResult.MatchingResult.MatchedItems,
            userId);

        // Map to mixed content result
        result.IsSuccess = playlistResult.IsSuccess;
        result.PlaylistId = playlistResult.PlaylistId;
        result.OperationType = playlistResult.OperationType;
        result.TotalItemCount = playlistResult.FinalItemCount;
        result.ErrorMessage = playlistResult.ErrorMessage;

        // Add mixed content specific metrics
        result.MovieCount = mixedResult.MovieItems.Count;
        result.EpisodeCount = mixedResult.EpisodeItems.Count;
        result.ChronologicalOrderMaintained = true;

        return result;
    }

    /// <summary>
    /// Analyzes the content type distribution in mixed content items.
    /// </summary>
    /// <param name="mixedItems">The mixed content items to analyze.</param>
    /// <returns>Content type analysis.</returns>
    private static ContentTypeAnalysis AnalyzeMixedContentDistribution(List<MixedContentItem> mixedItems)
    {
        var analysis = new ContentTypeAnalysis
        {
            TotalItems = mixedItems.Count
        };

        var movieCount = mixedItems.Count(item => item.ContentType == MixedContentType.Movie);
        var episodeCount = mixedItems.Count(item => item.ContentType == MixedContentType.Episode);
        var otherCount = mixedItems.Count(item => item.ContentType == MixedContentType.Other);

        analysis.MovieCount = movieCount;
        analysis.EpisodeCount = episodeCount;
        analysis.OtherCount = otherCount;
        analysis.IsMixedContent = movieCount > 0 && episodeCount > 0;

        analysis.ContentTypeCounts["movie"] = movieCount;
        analysis.ContentTypeCounts["episode"] = episodeCount;
        analysis.ContentTypeCounts["other"] = otherCount;

        if (mixedItems.Count > 0)
        {
            analysis.ContentTypePercentages["movie"] = (double)movieCount / mixedItems.Count * 100;
            analysis.ContentTypePercentages["episode"] = (double)episodeCount / mixedItems.Count * 100;
            analysis.ContentTypePercentages["other"] = (double)otherCount / mixedItems.Count * 100;
        }

        return analysis;
    }

    /// <summary>
    /// Validates that mixed content items are suitable for playlist creation.
    /// </summary>
    /// <param name="mixedItems">The mixed content items to validate.</param>
    /// <returns>True if valid for playlist creation, false otherwise.</returns>
    private bool ValidateMixedContentForPlaylist(List<MixedContentItem> mixedItems)
    {
        if (mixedItems == null || mixedItems.Count == 0)
        {
            _logger.LogWarning("No mixed content items provided for validation");
            return false;
        }

        var invalidItems = mixedItems.Where(item => 
            item == null || 
            item.ItemId == Guid.Empty || 
            item.ContentType == MixedContentType.Other).ToList();

        if (invalidItems.Count > 0)
        {
            _logger.LogWarning("Found {InvalidCount} invalid items in mixed content", invalidItems.Count);
            return false;
        }

        return true;
    }
}

/// <summary>
/// Represents a mixed content item for playlist creation.
/// </summary>
public class MixedContentItem
{
    /// <summary>
    /// Gets or sets the Jellyfin item ID.
    /// </summary>
    public Guid ItemId { get; set; }

    /// <summary>
    /// Gets or sets the content type.
    /// </summary>
    public MixedContentType ContentType { get; set; }

    /// <summary>
    /// Gets or sets the chronological position in the timeline.
    /// </summary>
    public int ChronologicalOrder { get; set; }

    /// <summary>
    /// Gets or sets optional metadata about the item.
    /// </summary>
    public string? Metadata { get; set; }
}

/// <summary>
/// Enumeration of mixed content types.
/// </summary>
public enum MixedContentType
{
    /// <summary>
    /// Movie content.
    /// </summary>
    Movie,

    /// <summary>
    /// TV episode content.
    /// </summary>
    Episode,

    /// <summary>
    /// Other or unsupported content type.
    /// </summary>
    Other
}

/// <summary>
/// Result of a mixed content playlist operation.
/// </summary>
public class MixedContentPlaylistResult
{
    /// <summary>
    /// Gets or sets the universe name.
    /// </summary>
    public string UniverseName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the operation was successful.
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Gets or sets the playlist ID (for successful operations).
    /// </summary>
    public Guid? PlaylistId { get; set; }

    /// <summary>
    /// Gets or sets the type of operation performed.
    /// </summary>
    public PlaylistOperationType OperationType { get; set; }

    /// <summary>
    /// Gets or sets the total number of items in the playlist.
    /// </summary>
    public int TotalItemCount { get; set; }

    /// <summary>
    /// Gets or sets the number of movie items.
    /// </summary>
    public int MovieCount { get; set; }

    /// <summary>
    /// Gets or sets the number of episode items.
    /// </summary>
    public int EpisodeCount { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether chronological order was maintained.
    /// </summary>
    public bool ChronologicalOrderMaintained { get; set; }

    /// <summary>
    /// Gets or sets the content type analysis.
    /// </summary>
    public ContentTypeAnalysis ContentTypeAnalysis { get; set; } = new();

    /// <summary>
    /// Gets or sets the error message (for failed operations).
    /// </summary>
    public string? ErrorMessage { get; set; }
}