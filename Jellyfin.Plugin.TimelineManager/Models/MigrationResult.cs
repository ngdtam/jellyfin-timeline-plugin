using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.TimelineManager.Models;

/// <summary>
/// Result of a configuration migration operation from legacy single-file to multi-file format.
/// </summary>
public class MigrationResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the migration succeeded.
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the number of universes successfully migrated.
    /// </summary>
    [JsonPropertyName("universesMigrated")]
    public int UniversesMigrated { get; set; }

    /// <summary>
    /// Gets or sets the list of error messages if the migration encountered issues.
    /// </summary>
    [JsonPropertyName("errors")]
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether the legacy configuration was backed up.
    /// </summary>
    [JsonPropertyName("backupCreated")]
    public bool BackupCreated { get; set; }
}
