using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.TimelineManager.Models;

/// <summary>
/// Represents a cinematic universe configuration with chronologically ordered content.
/// </summary>
public class Universe
{
    /// <summary>
    /// Gets or sets the unique identifier key for this universe (e.g., "mcu", "star_wars_canon").
    /// </summary>
    [Required]
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name for this universe (e.g., "Marvel Cinematic Universe").
    /// </summary>
    [Required]
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the chronologically ordered list of timeline items in this universe.
    /// </summary>
    [Required]
    [JsonPropertyName("items")]
    public List<TimelineItem> Items { get; set; } = new();
}