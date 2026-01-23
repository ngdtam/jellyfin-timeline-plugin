using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TimelineManager.Services;

/// <summary>
/// Service for efficient content lookup using Provider_ID-based dictionary indexing.
/// Provides O(1) lookup performance for large media libraries.
/// </summary>
public class ContentLookupService
{
    private readonly ILogger<ContentLookupService> _logger;
    private readonly ILibraryManager _libraryManager;

    // Separate dictionaries for different provider types and content types
    private readonly Dictionary<string, Guid> _tmdbMovieLookup = new();
    private readonly Dictionary<string, Guid> _tmdbShowLookup = new();
    private readonly Dictionary<string, Guid> _tmdbEpisodeLookup = new();
    private readonly Dictionary<string, Guid> _imdbMovieLookup = new();
    private readonly Dictionary<string, Guid> _imdbShowLookup = new();
    private readonly Dictionary<string, Guid> _imdbEpisodeLookup = new();

    private DateTime _lastBuilt = DateTime.MinValue;
    private int _totalItemsIndexed = 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentLookupService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="libraryManager">The Jellyfin library manager.</param>
    public ContentLookupService(ILogger<ContentLookupService> logger, ILibraryManager libraryManager)
    {
        _logger = logger;
        _libraryManager = libraryManager;
    }

    /// <summary>
    /// Gets the timestamp when the lookup tables were last built.
    /// </summary>
    public DateTime LastBuilt => _lastBuilt;

    /// <summary>
    /// Gets the total number of items indexed in the lookup tables.
    /// </summary>
    public int TotalItemsIndexed => _totalItemsIndexed;

    /// <summary>
    /// Builds the lookup dictionaries from all available library items.
    /// This method should be called before performing any lookups.
    /// </summary>
    public void BuildLookupTables()
    {
        try
        {
            _logger.LogInformation("Building content lookup tables from library");

            // Clear existing lookup tables
            ClearLookupTables();

            // Get all media items from the library
            var queryResult = _libraryManager.GetItemsResult(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { Jellyfin.Data.Enums.BaseItemKind.Movie, Jellyfin.Data.Enums.BaseItemKind.Series, Jellyfin.Data.Enums.BaseItemKind.Episode },
                Recursive = true
            });

            var libraryItems = queryResult.Items;
            _logger.LogDebug("Found {ItemCount} total items in library", libraryItems.Count);

            var indexedCount = 0;

            foreach (var item in libraryItems)
            {
                if (IndexItem(item))
                {
                    indexedCount++;
                }
            }

            _totalItemsIndexed = indexedCount;
            _lastBuilt = DateTime.UtcNow;

            _logger.LogInformation("Successfully built lookup tables with {IndexedCount} items. " +
                                 "TMDB Movies: {TmdbMovies}, TMDB Shows: {TmdbShows}, TMDB Episodes: {TmdbEpisodes}, " +
                                 "IMDB Movies: {ImdbMovies}, IMDB Shows: {ImdbShows}, IMDB Episodes: {ImdbEpisodes}",
                indexedCount,
                _tmdbMovieLookup.Count,
                _tmdbShowLookup.Count,
                _tmdbEpisodeLookup.Count,
                _imdbMovieLookup.Count,
                _imdbShowLookup.Count,
                _imdbEpisodeLookup.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build content lookup tables: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Finds a media item by its Provider_ID and provider name.
    /// </summary>
    /// <param name="providerId">The external provider ID (e.g., "1771", "tt0371746").</param>
    /// <param name="providerName">The provider name ("tmdb" or "imdb").</param>
    /// <param name="contentType">The content type ("movie" or "episode").</param>
    /// <returns>The internal Jellyfin item ID if found, null otherwise.</returns>
    public Guid? FindItemByProviderId(string providerId, string providerName, string contentType)
    {
        if (string.IsNullOrWhiteSpace(providerId) || string.IsNullOrWhiteSpace(providerName) || string.IsNullOrWhiteSpace(contentType))
        {
            return null;
        }

        var normalizedProvider = providerName.ToLowerInvariant();
        var normalizedType = contentType.ToLowerInvariant();

        try
        {
            // Select the appropriate lookup dictionary based on provider and content type
            var lookupDictionary = GetLookupDictionary(normalizedProvider, normalizedType);
            
            if (lookupDictionary == null)
            {
                _logger.LogWarning("Unsupported provider/type combination: {Provider}/{Type}", providerName, contentType);
                return null;
            }

            // Perform O(1) lookup
            if (lookupDictionary.TryGetValue(providerId, out var itemId))
            {
                _logger.LogDebug("Found item {ItemId} for {Provider}:{ProviderId} ({Type})", itemId, providerName, providerId, contentType);
                return itemId;
            }

            _logger.LogDebug("Item not found for {Provider}:{ProviderId} ({Type})", providerName, providerId, contentType);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during lookup for {Provider}:{ProviderId} ({Type}): {Message}", 
                providerName, providerId, contentType, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Finds multiple items by their Provider_IDs efficiently.
    /// </summary>
    /// <param name="providerItems">Collection of provider ID, provider name, and content type tuples.</param>
    /// <returns>Dictionary mapping provider keys to found item IDs.</returns>
    public Dictionary<string, Guid> FindItemsByProviderIds(IEnumerable<(string ProviderId, string ProviderName, string ContentType)> providerItems)
    {
        var results = new Dictionary<string, Guid>();

        foreach (var (providerId, providerName, contentType) in providerItems)
        {
            var itemId = FindItemByProviderId(providerId, providerName, contentType);
            if (itemId.HasValue)
            {
                var providerKey = $"{providerName.ToLowerInvariant()}_{providerId}";
                results[providerKey] = itemId.Value;
            }
        }

        return results;
    }

    /// <summary>
    /// Clears all lookup tables and resets statistics.
    /// </summary>
    public void ClearLookupTables()
    {
        _tmdbMovieLookup.Clear();
        _tmdbShowLookup.Clear();
        _tmdbEpisodeLookup.Clear();
        _imdbMovieLookup.Clear();
        _imdbShowLookup.Clear();
        _imdbEpisodeLookup.Clear();

        _totalItemsIndexed = 0;
        _lastBuilt = DateTime.MinValue;

        _logger.LogDebug("Cleared all lookup tables");
    }

    /// <summary>
    /// Gets lookup statistics for monitoring and debugging.
    /// </summary>
    /// <returns>A dictionary containing lookup table statistics.</returns>
    public Dictionary<string, object> GetLookupStatistics()
    {
        return new Dictionary<string, object>
        {
            ["LastBuilt"] = _lastBuilt,
            ["TotalItemsIndexed"] = _totalItemsIndexed,
            ["TmdbMovieCount"] = _tmdbMovieLookup.Count,
            ["TmdbShowCount"] = _tmdbShowLookup.Count,
            ["TmdbEpisodeCount"] = _tmdbEpisodeLookup.Count,
            ["ImdbMovieCount"] = _imdbMovieLookup.Count,
            ["ImdbShowCount"] = _imdbShowLookup.Count,
            ["ImdbEpisodeCount"] = _imdbEpisodeLookup.Count
        };
    }

    /// <summary>
    /// Indexes a single library item into the appropriate lookup dictionaries.
    /// </summary>
    /// <param name="item">The library item to index.</param>
    /// <returns>True if the item was successfully indexed, false otherwise.</returns>
    private bool IndexItem(BaseItem item)
    {
        if (item?.ProviderIds == null || item.ProviderIds.Count == 0)
        {
            return false;
        }

        var indexed = false;

        // Index TMDB IDs
        if (item.ProviderIds.TryGetValue("Tmdb", out var tmdbId) && !string.IsNullOrWhiteSpace(tmdbId))
        {
            var lookupDict = GetLookupDictionaryForItem(item, "tmdb");
            if (lookupDict != null)
            {
                lookupDict[tmdbId] = item.Id;
                indexed = true;
                _logger.LogTrace("Indexed {ItemType} '{ItemName}' with TMDB ID {TmdbId}", 
                    item.GetType().Name, item.Name, tmdbId);
            }
        }

        // Index IMDB IDs
        if (item.ProviderIds.TryGetValue("Imdb", out var imdbId) && !string.IsNullOrWhiteSpace(imdbId))
        {
            var lookupDict = GetLookupDictionaryForItem(item, "imdb");
            if (lookupDict != null)
            {
                lookupDict[imdbId] = item.Id;
                indexed = true;
                _logger.LogTrace("Indexed {ItemType} '{ItemName}' with IMDB ID {ImdbId}", 
                    item.GetType().Name, item.Name, imdbId);
            }
        }

        return indexed;
    }

    /// <summary>
    /// Gets the appropriate lookup dictionary for a specific provider and content type.
    /// </summary>
    /// <param name="providerName">The provider name (normalized to lowercase).</param>
    /// <param name="contentType">The content type (normalized to lowercase).</param>
    /// <returns>The appropriate lookup dictionary, or null if not supported.</returns>
    private Dictionary<string, Guid>? GetLookupDictionary(string providerName, string contentType)
    {
        return (providerName, contentType) switch
        {
            ("tmdb", "movie") => _tmdbMovieLookup,
            ("tmdb", "episode") => _tmdbEpisodeLookup,
            ("imdb", "movie") => _imdbMovieLookup,
            ("imdb", "episode") => _imdbEpisodeLookup,
            _ => null
        };
    }

    /// <summary>
    /// Gets the appropriate lookup dictionary for a library item based on its type and provider.
    /// </summary>
    /// <param name="item">The library item.</param>
    /// <param name="providerName">The provider name (normalized to lowercase).</param>
    /// <returns>The appropriate lookup dictionary, or null if not supported.</returns>
    private Dictionary<string, Guid>? GetLookupDictionaryForItem(BaseItem item, string providerName)
    {
        return (item, providerName) switch
        {
            (Movie, "tmdb") => _tmdbMovieLookup,
            (Movie, "imdb") => _imdbMovieLookup,
            (Series, "tmdb") => _tmdbShowLookup,
            (Series, "imdb") => _imdbShowLookup,
            (Episode, "tmdb") => _tmdbEpisodeLookup,
            (Episode, "imdb") => _imdbEpisodeLookup,
            _ => null
        };
    }
}