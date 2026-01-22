using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.TimelineManager.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TimelineManager.Services;

/// <summary>
/// Service for matching timeline items with library content using Provider_ID-based matching.
/// Prioritizes Provider_ID matching over title or other attribute matching for accuracy.
/// </summary>
public class ProviderMatchingService
{
    private readonly ILogger<ProviderMatchingService> _logger;
    private readonly ContentLookupService _contentLookupService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderMatchingService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="contentLookupService">The content lookup service for Provider_ID resolution.</param>
    public ProviderMatchingService(ILogger<ProviderMatchingService> logger, ContentLookupService contentLookupService)
    {
        _logger = logger;
        _contentLookupService = contentLookupService;
    }

    /// <summary>
    /// Matches a single timeline item with library content using Provider_ID.
    /// </summary>
    /// <param name="timelineItem">The timeline item to match.</param>
    /// <returns>The matched library item ID if found, null otherwise.</returns>
    public Guid? MatchTimelineItem(TimelineItem timelineItem)
    {
        if (timelineItem == null)
        {
            _logger.LogWarning("Cannot match null timeline item");
            return null;
        }

        if (string.IsNullOrWhiteSpace(timelineItem.ProviderId) || 
            string.IsNullOrWhiteSpace(timelineItem.ProviderName) || 
            string.IsNullOrWhiteSpace(timelineItem.Type))
        {
            _logger.LogWarning("Timeline item has missing required fields: ProviderId='{ProviderId}', ProviderName='{ProviderName}', Type='{Type}'",
                timelineItem.ProviderId, timelineItem.ProviderName, timelineItem.Type);
            return null;
        }

        try
        {
            // Use Provider_ID matching as the primary and only matching method
            var matchedId = _contentLookupService.FindItemByProviderId(
                timelineItem.ProviderId, 
                timelineItem.ProviderName, 
                timelineItem.Type);

            if (matchedId.HasValue)
            {
                _logger.LogDebug("Successfully matched timeline item {ProviderKey} to library item {ItemId}",
                    timelineItem.ProviderKey, matchedId.Value);
                return matchedId.Value;
            }

            _logger.LogWarning("No library item found for Provider_ID {ProviderKey} ({ProviderName}:{ProviderId}, Type: {Type})",
                timelineItem.ProviderKey, timelineItem.ProviderName, timelineItem.ProviderId, timelineItem.Type);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error matching timeline item {ProviderKey}: {Message}",
                timelineItem.ProviderKey, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Matches multiple timeline items efficiently using batch Provider_ID lookup.
    /// </summary>
    /// <param name="timelineItems">The collection of timeline items to match.</param>
    /// <returns>A dictionary mapping timeline items to their matched library item IDs.</returns>
    public Dictionary<TimelineItem, Guid> MatchTimelineItems(IEnumerable<TimelineItem> timelineItems)
    {
        var results = new Dictionary<TimelineItem, Guid>();
        var validItems = timelineItems?.Where(IsValidTimelineItem).ToList() ?? new List<TimelineItem>();

        if (validItems.Count == 0)
        {
            _logger.LogDebug("No valid timeline items to match");
            return results;
        }

        _logger.LogDebug("Matching {ItemCount} timeline items using Provider_ID lookup", validItems.Count);

        // Prepare provider lookup data
        var providerLookupData = validItems.Select(item => (
            ProviderId: item.ProviderId,
            ProviderName: item.ProviderName,
            ContentType: item.Type
        )).ToList();

        // Perform batch lookup for better performance
        var lookupResults = _contentLookupService.FindItemsByProviderIds(providerLookupData);

        // Map results back to timeline items
        foreach (var item in validItems)
        {
            if (lookupResults.TryGetValue(item.ProviderKey, out var libraryItemId))
            {
                results[item] = libraryItemId;
                _logger.LogTrace("Matched timeline item {ProviderKey} to library item {ItemId}",
                    item.ProviderKey, libraryItemId);
            }
            else
            {
                _logger.LogDebug("No match found for timeline item {ProviderKey}",
                    item.ProviderKey);
            }
        }

        _logger.LogInformation("Successfully matched {MatchedCount} out of {TotalCount} timeline items",
            results.Count, validItems.Count);

        return results;
    }

    /// <summary>
    /// Matches timeline items for a specific universe and returns ordered library item IDs.
    /// </summary>
    /// <param name="universe">The universe containing timeline items to match.</param>
    /// <returns>A result containing matched items, missing items, and statistics.</returns>
    public UniverseMatchingResult MatchUniverseItems(Universe universe)
    {
        if (universe == null)
        {
            throw new ArgumentNullException(nameof(universe));
        }

        var result = new UniverseMatchingResult
        {
            UniverseKey = universe.Key,
            UniverseName = universe.Name
        };

        if (universe.Items == null || universe.Items.Count == 0)
        {
            _logger.LogWarning("Universe '{UniverseName}' has no timeline items to match", universe.Name);
            return result;
        }

        _logger.LogInformation("Matching {ItemCount} timeline items for universe '{UniverseName}'",
            universe.Items.Count, universe.Name);

        var matchingResults = MatchTimelineItems(universe.Items);

        // Build ordered list of matched items maintaining chronological order
        foreach (var timelineItem in universe.Items)
        {
            if (matchingResults.TryGetValue(timelineItem, out var libraryItemId))
            {
                result.MatchedItems.Add(libraryItemId);
                result.MatchedTimelineItems.Add(timelineItem);
            }
            else
            {
                result.MissingItems.Add(timelineItem.ProviderKey);
                result.MissingTimelineItems.Add(timelineItem);
            }
        }

        result.MatchingStatistics = new MatchingStatistics
        {
            TotalItems = universe.Items.Count,
            MatchedItems = result.MatchedItems.Count,
            MissingItems = result.MissingItems.Count,
            MatchingRate = universe.Items.Count > 0 ? (double)result.MatchedItems.Count / universe.Items.Count : 0.0
        };

        _logger.LogInformation("Universe '{UniverseName}' matching completed: {MatchedCount}/{TotalCount} items matched ({MatchingRate:P1})",
            universe.Name, result.MatchedItems.Count, universe.Items.Count, result.MatchingStatistics.MatchingRate);

        if (result.MissingItems.Count > 0)
        {
            _logger.LogWarning("Universe '{UniverseName}' has {MissingCount} missing items: {MissingItems}",
                universe.Name, result.MissingItems.Count, string.Join(", ", result.MissingItems));
        }

        return result;
    }

    /// <summary>
    /// Validates that a timeline item has all required fields for Provider_ID matching.
    /// </summary>
    /// <param name="item">The timeline item to validate.</param>
    /// <returns>True if the item is valid for matching, false otherwise.</returns>
    private static bool IsValidTimelineItem(TimelineItem item)
    {
        return item != null &&
               !string.IsNullOrWhiteSpace(item.ProviderId) &&
               !string.IsNullOrWhiteSpace(item.ProviderName) &&
               !string.IsNullOrWhiteSpace(item.Type);
    }
}

/// <summary>
/// Result of matching timeline items for a universe.
/// </summary>
public class UniverseMatchingResult
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
    /// Gets the list of matched library item IDs in chronological order.
    /// </summary>
    public List<Guid> MatchedItems { get; } = new();

    /// <summary>
    /// Gets the list of matched timeline items in chronological order.
    /// </summary>
    public List<TimelineItem> MatchedTimelineItems { get; } = new();

    /// <summary>
    /// Gets the list of missing item provider keys.
    /// </summary>
    public List<string> MissingItems { get; } = new();

    /// <summary>
    /// Gets the list of missing timeline items.
    /// </summary>
    public List<TimelineItem> MissingTimelineItems { get; } = new();

    /// <summary>
    /// Gets or sets the matching statistics.
    /// </summary>
    public MatchingStatistics MatchingStatistics { get; set; } = new();
}

/// <summary>
/// Statistics about the matching process.
/// </summary>
public class MatchingStatistics
{
    /// <summary>
    /// Gets or sets the total number of timeline items.
    /// </summary>
    public int TotalItems { get; set; }

    /// <summary>
    /// Gets or sets the number of successfully matched items.
    /// </summary>
    public int MatchedItems { get; set; }

    /// <summary>
    /// Gets or sets the number of missing items.
    /// </summary>
    public int MissingItems { get; set; }

    /// <summary>
    /// Gets or sets the matching rate (0.0 to 1.0).
    /// </summary>
    public double MatchingRate { get; set; }
}