using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.TimelineManager.Models;

/// <summary>
/// Represents the result of creating or updating a single playlist.
/// </summary>
public class PlaylistResult
{
    /// <summary>
    /// Gets or sets the name of the playlist.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the action performed on the playlist.
    /// </summary>
    /// <remarks>
    /// Valid values: "created" or "updated".
    /// </remarks>
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of items successfully added to the playlist.
    /// </summary>
    [JsonPropertyName("itemsAdded")]
    public int ItemsAdded { get; set; }

    /// <summary>
    /// Gets or sets the number of items that were missing from the library.
    /// </summary>
    [JsonPropertyName("itemsMissing")]
    public int ItemsMissing { get; set; }

    /// <summary>
    /// Gets or sets the list of missing items with their names and provider information.
    /// </summary>
    [JsonPropertyName("missingItems")]
    public List<string> MissingItems { get; set; } = new();
}
