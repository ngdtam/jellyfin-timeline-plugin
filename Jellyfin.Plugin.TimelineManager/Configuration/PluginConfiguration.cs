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
        TmdbApiKey = string.Empty;
    }

    /// <summary>
    /// Gets or sets the TMDB API key for searching external content.
    /// </summary>
    public string TmdbApiKey { get; set; }
}