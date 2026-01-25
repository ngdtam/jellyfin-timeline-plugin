using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.TimelineManager.Models;

/// <summary>
/// Represents a single search result item from the Jellyfin library.
/// </summary>
public class SearchResultItem
{
    /// <summary>
    /// Gets or sets the unique identifier for this media item.
    /// </summary>
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the title of the media item.
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the release year of the media item (optional).
    /// </summary>
    [JsonPropertyName("year")]
    public int? Year { get; set; }

    /// <summary>
    /// Gets or sets the type of media item (e.g., "Movie" or "Episode").
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the dictionary of provider IDs (e.g., "tmdb", "imdb").
    /// </summary>
    [JsonPropertyName("providerIds")]
    public Dictionary<string, string> ProviderIds { get; set; } = new();

    /// <summary>
    /// Gets or sets the season number for TV episodes (optional, only for episodes).
    /// </summary>
    [JsonPropertyName("seasonNumber")]
    public int? SeasonNumber { get; set; }

    /// <summary>
    /// Gets or sets the series name for TV episodes (optional, only for episodes).
    /// </summary>
    [JsonPropertyName("seriesName")]
    public string SeriesName { get; set; } = string.Empty;
}
