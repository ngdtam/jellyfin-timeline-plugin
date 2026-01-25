using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.TimelineManager.Models;

/// <summary>
/// Lightweight metadata for a universe file, used for listing available universes.
/// </summary>
public class UniverseMetadata
{
    /// <summary>
    /// Gets or sets the unique identifier key for this universe (e.g., "mcu", "star-wars").
    /// </summary>
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name for this universe (e.g., "Marvel Cinematic Universe").
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the filename of the universe file (e.g., "mcu.json").
    /// </summary>
    [JsonPropertyName("filename")]
    public string Filename { get; set; } = string.Empty;
}
