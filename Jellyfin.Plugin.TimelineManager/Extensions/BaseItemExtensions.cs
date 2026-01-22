using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;

namespace Jellyfin.Plugin.TimelineManager.Extensions;

/// <summary>
/// Extension methods for BaseItem.
/// </summary>
public static class BaseItemExtensions
{
    /// <summary>
    /// Gets the season number for an episode.
    /// </summary>
    /// <param name="item">The base item.</param>
    /// <returns>Season number or null.</returns>
    public static int? GetSeasonNumber(this BaseItem item)
    {
        if (item is Episode episode)
        {
            return episode.ParentIndexNumber;
        }
        return null;
    }

    /// <summary>
    /// Gets the episode number for an episode.
    /// </summary>
    /// <param name="item">The base item.</param>
    /// <returns>Episode number or null.</returns>
    public static int? GetEpisodeNumber(this BaseItem item)
    {
        if (item is Episode episode)
        {
            return episode.IndexNumber;
        }
        return null;
    }

    /// <summary>
    /// Gets the parent item (series for episodes).
    /// </summary>
    /// <param name="item">The base item.</param>
    /// <returns>Parent item or null.</returns>
    public static BaseItem? GetParentItem(this BaseItem item)
    {
        return item.GetParent();
    }
}