using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.TimelineManager.Models;

/// <summary>
/// Response model for playlist creation operations.
/// </summary>
public class PlaylistCreationResponse
{
    /// <summary>
    /// Gets or sets a value indicating whether the playlist creation was successful.
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the response message summarizing the operation result.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of playlist results for each universe processed.
    /// </summary>
    [JsonPropertyName("playlists")]
    public List<PlaylistResult> Playlists { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of errors that occurred during playlist creation.
    /// </summary>
    [JsonPropertyName("errors")]
    public List<string> Errors { get; set; } = new();
}
