using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.TimelineManager.Models;

/// <summary>
/// Result of a universe save operation, including success status and any validation errors.
/// </summary>
public class SaveUniverseResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the save operation succeeded.
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the list of error messages if the save operation failed.
    /// </summary>
    [JsonPropertyName("errors")]
    public List<string> Errors { get; set; } = new();
}
