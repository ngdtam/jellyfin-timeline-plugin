using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.TimelineManager.Models;

/// <summary>
/// Represents a single media item in a timeline with provider identification.
/// </summary>
public class TimelineItem
{
    /// <summary>
    /// Gets or sets the external provider ID (e.g., "1771" for TMDB, "tt0371746" for IMDB).
    /// </summary>
    [Required]
    [JsonPropertyName("providerId")]
    public string ProviderId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the provider name (e.g., "tmdb", "imdb").
    /// </summary>
    [Required]
    [JsonPropertyName("providerName")]
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the content type (e.g., "movie", "episode").
    /// </summary>
    [Required]
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets the formatted provider key for lookup operations (e.g., "tmdb_1771").
    /// </summary>
    [JsonIgnore]
    public string ProviderKey => $"{ProviderName.ToLowerInvariant()}_{ProviderId}";
}