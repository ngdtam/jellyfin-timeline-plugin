using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.TimelineManager.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TimelineManager.Services;

/// <summary>
/// Service for handling mixed content types (movies and TV episodes) within the same universe timeline.
/// Ensures proper processing and validation of heterogeneous content collections.
/// </summary>
public class MixedContentService
{
    private readonly ILogger<MixedContentService> _logger;
    private readonly ProviderMatchingService _providerMatchingService;

    /// <summary>
    /// Initializes a new instance of the <see cref="MixedContentService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="providerMatchingService">The provider matching service.</param>
    public MixedContentService(ILogger<MixedContentService> logger, ProviderMatchingService providerMatchingService)
    {
        _logger = logger;
        _providerMatchingService = providerMatchingService;
    }

    /// <summary>
    /// Processes a universe containing mixed content types and validates compatibility.
    /// </summary>
    /// <param name="universe">The universe to process.</param>
    /// <returns>A result containing processed mixed content information.</returns>
    public MixedContentResult ProcessMixedContentUniverse(Universe universe)
    {
        if (universe == null)
        {
            throw new ArgumentNullException(nameof(universe));
        }

        var result = new MixedContentResult
        {
            UniverseKey = universe.Key,
            UniverseName = universe.Name
        };

        if (universe.Items == null || universe.Items.Count == 0)
        {
            _logger.LogWarning("Universe '{UniverseName}' has no items to process", universe.Name);
            return result;
        }

        _logger.LogInformation("Processing mixed content universe '{UniverseName}' with {ItemCount} items",
            universe.Name, universe.Items.Count);

        // Analyze content type distribution
        var contentTypeAnalysis = AnalyzeContentTypes(universe.Items);
        result.ContentTypeAnalysis = contentTypeAnalysis;

        // Validate mixed content compatibility
        var validationResult = ValidateMixedContentCompatibility(universe.Items);
        result.ValidationResult = validationResult;

        if (!validationResult.IsValid)
        {
            _logger.LogError("Mixed content validation failed for universe '{UniverseName}': {Errors}",
                universe.Name, string.Join(", ", validationResult.Errors));
            return result;
        }

        // Process matching for mixed content
        var matchingResult = _providerMatchingService.MatchUniverseItems(universe);
        result.MatchingResult = matchingResult;

        // Organize results by content type
        OrganizeResultsByContentType(result, matchingResult);

        _logger.LogInformation("Mixed content processing completed for universe '{UniverseName}': " +
                             "{MovieCount} movies, {EpisodeCount} episodes, {TotalMatched}/{TotalItems} matched",
            universe.Name, result.MovieItems.Count, result.EpisodeItems.Count,
            matchingResult.MatchedItems.Count, universe.Items.Count);

        return result;
    }

    /// <summary>
    /// Validates that mixed content types can be processed together in a single universe.
    /// </summary>
    /// <param name="timelineItems">The timeline items to validate.</param>
    /// <returns>A validation result indicating compatibility.</returns>
    public MixedContentValidationResult ValidateMixedContentCompatibility(List<TimelineItem> timelineItems)
    {
        var result = new MixedContentValidationResult { IsValid = true };

        if (timelineItems == null || timelineItems.Count == 0)
        {
            result.Errors.Add("No timeline items provided for validation");
            result.IsValid = false;
            return result;
        }

        var supportedTypes = new HashSet<string> { "movie", "episode" };
        var foundTypes = new HashSet<string>();
        var invalidItems = new List<string>();

        foreach (var item in timelineItems)
        {
            if (item == null)
            {
                invalidItems.Add("Null timeline item found");
                continue;
            }

            if (string.IsNullOrWhiteSpace(item.Type))
            {
                invalidItems.Add($"Timeline item with Provider_ID '{item.ProviderId}' has no content type");
                continue;
            }

            var normalizedType = item.Type.ToLowerInvariant();
            foundTypes.Add(normalizedType);

            if (!supportedTypes.Contains(normalizedType))
            {
                invalidItems.Add($"Unsupported content type '{item.Type}' for item '{item.ProviderKey}'");
            }

            // Validate provider compatibility with content type
            if (!IsProviderCompatibleWithContentType(item.ProviderName, normalizedType))
            {
                invalidItems.Add($"Provider '{item.ProviderName}' is not compatible with content type '{item.Type}' for item '{item.ProviderKey}'");
            }
        }

        if (invalidItems.Count > 0)
        {
            result.Errors.AddRange(invalidItems);
            result.IsValid = false;
        }

        // Log mixed content type information
        if (foundTypes.Count > 1)
        {
            _logger.LogDebug("Mixed content types detected: {ContentTypes}", string.Join(", ", foundTypes));
            result.IsMixedContent = true;
        }
        else if (foundTypes.Count == 1)
        {
            _logger.LogDebug("Homogeneous content type: {ContentType}", foundTypes.First());
            result.IsMixedContent = false;
        }

        result.DetectedContentTypes = foundTypes.ToList();

        return result;
    }

    /// <summary>
    /// Analyzes the distribution of content types within a collection of timeline items.
    /// </summary>
    /// <param name="timelineItems">The timeline items to analyze.</param>
    /// <returns>Analysis results showing content type distribution.</returns>
    public ContentTypeAnalysis AnalyzeContentTypes(List<TimelineItem> timelineItems)
    {
        var analysis = new ContentTypeAnalysis();

        if (timelineItems == null || timelineItems.Count == 0)
        {
            return analysis;
        }

        var typeGroups = timelineItems
            .Where(item => item != null && !string.IsNullOrWhiteSpace(item.Type))
            .GroupBy(item => item.Type.ToLowerInvariant())
            .ToList();

        foreach (var group in typeGroups)
        {
            var contentType = group.Key;
            var count = group.Count();
            var percentage = (double)count / timelineItems.Count * 100;

            analysis.ContentTypeCounts[contentType] = count;
            analysis.ContentTypePercentages[contentType] = percentage;

            switch (contentType)
            {
                case "movie":
                    analysis.MovieCount = count;
                    break;
                case "episode":
                    analysis.EpisodeCount = count;
                    break;
                default:
                    analysis.OtherCount += count;
                    break;
            }
        }

        analysis.TotalItems = timelineItems.Count;
        analysis.IsMixedContent = analysis.ContentTypeCounts.Count > 1;

        return analysis;
    }

    /// <summary>
    /// Checks if a provider is compatible with a specific content type.
    /// </summary>
    /// <param name="providerName">The provider name.</param>
    /// <param name="contentType">The content type.</param>
    /// <returns>True if compatible, false otherwise.</returns>
    private static bool IsProviderCompatibleWithContentType(string providerName, string contentType)
    {
        if (string.IsNullOrWhiteSpace(providerName) || string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        var normalizedProvider = providerName.ToLowerInvariant();
        var normalizedType = contentType.ToLowerInvariant();

        return (normalizedProvider, normalizedType) switch
        {
            ("tmdb", "movie") => true,
            ("tmdb", "episode") => true,
            ("imdb", "movie") => true,
            ("imdb", "episode") => true,
            _ => false
        };
    }

    /// <summary>
    /// Organizes matching results by content type for easier processing.
    /// </summary>
    /// <param name="result">The mixed content result to populate.</param>
    /// <param name="matchingResult">The matching result to organize.</param>
    private static void OrganizeResultsByContentType(MixedContentResult result, UniverseMatchingResult matchingResult)
    {
        for (int i = 0; i < matchingResult.MatchedTimelineItems.Count; i++)
        {
            var timelineItem = matchingResult.MatchedTimelineItems[i];
            var libraryItemId = matchingResult.MatchedItems[i];

            var contentType = timelineItem.Type.ToLowerInvariant();
            switch (contentType)
            {
                case "movie":
                    result.MovieItems.Add(libraryItemId);
                    break;
                case "episode":
                    result.EpisodeItems.Add(libraryItemId);
                    break;
                default:
                    result.OtherItems.Add(libraryItemId);
                    break;
            }
        }
    }
}

/// <summary>
/// Result of processing mixed content within a universe.
/// </summary>
public class MixedContentResult
{
    /// <summary>
    /// Gets or sets the universe key.
    /// </summary>
    public string UniverseKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the universe name.
    /// </summary>
    public string UniverseName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the content type analysis.
    /// </summary>
    public ContentTypeAnalysis ContentTypeAnalysis { get; set; } = new();

    /// <summary>
    /// Gets or sets the mixed content validation result.
    /// </summary>
    public MixedContentValidationResult ValidationResult { get; set; } = new();

    /// <summary>
    /// Gets or sets the provider matching result.
    /// </summary>
    public UniverseMatchingResult MatchingResult { get; set; } = new();

    /// <summary>
    /// Gets the list of matched movie item IDs.
    /// </summary>
    public List<Guid> MovieItems { get; } = new();

    /// <summary>
    /// Gets the list of matched episode item IDs.
    /// </summary>
    public List<Guid> EpisodeItems { get; } = new();

    /// <summary>
    /// Gets the list of other content type item IDs.
    /// </summary>
    public List<Guid> OtherItems { get; } = new();
}

/// <summary>
/// Analysis of content type distribution within a timeline.
/// </summary>
public class ContentTypeAnalysis
{
    /// <summary>
    /// Gets the total number of items analyzed.
    /// </summary>
    public int TotalItems { get; set; }

    /// <summary>
    /// Gets the number of movie items.
    /// </summary>
    public int MovieCount { get; set; }

    /// <summary>
    /// Gets the number of episode items.
    /// </summary>
    public int EpisodeCount { get; set; }

    /// <summary>
    /// Gets the number of other content type items.
    /// </summary>
    public int OtherCount { get; set; }

    /// <summary>
    /// Gets a value indicating whether the content is mixed (multiple types).
    /// </summary>
    public bool IsMixedContent { get; set; }

    /// <summary>
    /// Gets the count of items by content type.
    /// </summary>
    public Dictionary<string, int> ContentTypeCounts { get; } = new();

    /// <summary>
    /// Gets the percentage distribution by content type.
    /// </summary>
    public Dictionary<string, double> ContentTypePercentages { get; } = new();
}

/// <summary>
/// Result of validating mixed content compatibility.
/// </summary>
public class MixedContentValidationResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the mixed content is valid.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the content contains multiple types.
    /// </summary>
    public bool IsMixedContent { get; set; }

    /// <summary>
    /// Gets the list of detected content types.
    /// </summary>
    public List<string> DetectedContentTypes { get; set; } = new();

    /// <summary>
    /// Gets the list of validation errors.
    /// </summary>
    public List<string> Errors { get; } = new();
}