using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.TimelineManager.Models;

/// <summary>
/// Root configuration model for timeline manager containing multiple universes.
/// </summary>
public class TimelineConfiguration
{
    /// <summary>
    /// Gets or sets the list of configured universes.
    /// </summary>
    [Required]
    [JsonPropertyName("universes")]
    public List<Universe> Universes { get; set; } = new();
}