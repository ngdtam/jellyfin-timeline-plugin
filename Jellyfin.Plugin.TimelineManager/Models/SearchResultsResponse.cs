using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.TimelineManager.Models;

/// <summary>
/// Response model for content search operations.
/// </summary>
public class SearchResultsResponse
{
    /// <summary>
    /// Gets or sets the list of search result items.
    /// </summary>
    [JsonPropertyName("results")]
    public List<SearchResultItem> Results { get; set; } = new();

    /// <summary>
    /// Gets or sets the total count of search results.
    /// </summary>
    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }
}
