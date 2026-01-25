using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.TimelineManager.Models;

/// <summary>
/// Represents a TV show search result from TMDB API.
/// </summary>
public class TmdbTvSearchResult
{
    /// <summary>
    /// Gets or sets the TMDB TV show ID.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the TV show name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the original TV show name.
    /// </summary>
    [JsonPropertyName("original_name")]
    public string OriginalName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the first air date in format "YYYY-MM-DD".
    /// </summary>
    [JsonPropertyName("first_air_date")]
    public string FirstAirDate { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the TV show overview/description.
    /// </summary>
    [JsonPropertyName("overview")]
    public string Overview { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the poster path.
    /// </summary>
    [JsonPropertyName("poster_path")]
    public string? PosterPath { get; set; }

    /// <summary>
    /// Gets or sets the backdrop path.
    /// </summary>
    [JsonPropertyName("backdrop_path")]
    public string? BackdropPath { get; set; }

    /// <summary>
    /// Gets or sets the vote average rating.
    /// </summary>
    [JsonPropertyName("vote_average")]
    public double VoteAverage { get; set; }

    /// <summary>
    /// Gets or sets the vote count.
    /// </summary>
    [JsonPropertyName("vote_count")]
    public int VoteCount { get; set; }

    /// <summary>
    /// Gets or sets the popularity score.
    /// </summary>
    [JsonPropertyName("popularity")]
    public double Popularity { get; set; }
}
