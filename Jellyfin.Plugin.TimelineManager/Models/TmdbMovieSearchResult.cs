using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.TimelineManager.Models;

/// <summary>
/// Represents a movie search result from TMDB API.
/// </summary>
public class TmdbMovieSearchResult
{
    /// <summary>
    /// Gets or sets the TMDB movie ID.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the movie title.
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the original movie title.
    /// </summary>
    [JsonPropertyName("original_title")]
    public string OriginalTitle { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the release date in format "YYYY-MM-DD".
    /// </summary>
    [JsonPropertyName("release_date")]
    public string ReleaseDate { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the movie overview/description.
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
