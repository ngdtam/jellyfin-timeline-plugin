using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.TimelineManager;

/// <summary>
/// Plugin configuration for the Universal Timeline Manager.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        JellyfinApiKey = string.Empty;
    }

    /// <summary>
    /// Gets or sets the Jellyfin API key for playlist operations.
    /// </summary>
    public string JellyfinApiKey { get; set; }
}