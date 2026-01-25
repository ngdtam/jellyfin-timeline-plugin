using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.TimelineManager.Models;

/// <summary>
/// Request model for creating playlists with optional universe selection.
/// </summary>
public class CreatePlaylistsRequest
{
    /// <summary>
    /// Gets or sets the list of selected universe filenames to process.
    /// If null or empty, all universes will be processed.
    /// </summary>
    [JsonPropertyName("selectedUniverseFilenames")]
    public List<string>? SelectedUniverseFilenames { get; set; }
}
