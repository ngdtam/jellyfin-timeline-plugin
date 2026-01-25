using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.TimelineManager.Models;

/// <summary>
/// Generic wrapper for TMDB API search responses.
/// </summary>
/// <typeparam name="T">The type of search result (movie or TV show).</typeparam>
public class TmdbSearchResponse<T>
{
    /// <summary>
    /// Gets or sets the list of search results.
    /// </summary>
    [JsonPropertyName("results")]
    public List<T> Results { get; set; } = new();

    /// <summary>
    /// Gets or sets the current page number.
    /// </summary>
    [JsonPropertyName("page")]
    public int Page { get; set; }

    /// <summary>
    /// Gets or sets the total number of pages.
    /// </summary>
    [JsonPropertyName("total_pages")]
    public int TotalPages { get; set; }

    /// <summary>
    /// Gets or sets the total number of results.
    /// </summary>
    [JsonPropertyName("total_results")]
    public int TotalResults { get; set; }
}
