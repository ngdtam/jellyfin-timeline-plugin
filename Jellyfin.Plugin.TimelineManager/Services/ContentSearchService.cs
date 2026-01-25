using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Plugin.TimelineManager.Models;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TimelineManager.Services;

/// <summary>
/// Service for searching and enriching media items from Jellyfin library.
/// Provides search capabilities by title and provider ID for the UI Playlist Creator.
/// </summary>
public class ContentSearchService
{
    private readonly ILogger<ContentSearchService> _logger;
    private readonly ILibraryManager _libraryManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentSearchService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="libraryManager">The Jellyfin library manager.</param>
    public ContentSearchService(ILogger<ContentSearchService> logger, ILibraryManager libraryManager)
    {
        _logger = logger;
        _libraryManager = libraryManager;
    }

    /// <summary>
    /// Searches the Jellyfin library for movies and episodes matching the specified title.
    /// </summary>
    /// <param name="query">The search query string to match against titles.</param>
    /// <param name="limit">The maximum number of results to return (default 20).</param>
    /// <returns>A list of enriched SearchResultItem objects with provider IDs.</returns>
    public async Task<List<SearchResultItem>> SearchByTitle(string query, int limit = 20)
    {
        var results = new List<SearchResultItem>();

        if (string.IsNullOrWhiteSpace(query))
        {
            _logger.LogDebug("Empty search query provided, returning empty results");
            return results;
        }

        try
        {
            _logger.LogDebug("Searching library for title: {Query} with limit: {Limit}", query, limit);

            // Query Jellyfin library for movies and episodes matching the title
            var queryResult = _libraryManager.GetItemsResult(new InternalItemsQuery
            {
                SearchTerm = query,
                IncludeItemTypes = new[] { Jellyfin.Data.Enums.BaseItemKind.Movie, Jellyfin.Data.Enums.BaseItemKind.Episode },
                Recursive = true,
                Limit = limit
            });

            var items = queryResult.Items;
            _logger.LogDebug("Found {ItemCount} items matching query: {Query}", items.Count, query);

            // Convert each item to SearchResultItem with enriched data
            foreach (var item in items)
            {
                var searchResultItem = new SearchResultItem
                {
                    Id = item.Id,
                    Title = item.Name ?? string.Empty,
                    Year = item.ProductionYear,
                    Type = item switch
                    {
                        Movie => "Movie",
                        Episode => "Episode",
                        _ => item.GetType().Name
                    },
                    ProviderIds = ExtractProviderIds(item)
                };

                // Add episode-specific information
                if (item is Episode episode)
                {
                    searchResultItem.SeasonNumber = episode.ParentIndexNumber;
                    searchResultItem.SeriesName = episode.SeriesName ?? string.Empty;
                }

                results.Add(searchResultItem);
            }

            _logger.LogInformation("Successfully returned {ResultCount} search results for query: {Query}", results.Count, query);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching library for query: {Query}. Message: {Message}", query, ex.Message);
            return results; // Return empty list on error
        }
    }

    /// <summary>
    /// Searches for a specific item by provider ID and provider name.
    /// </summary>
    /// <param name="providerId">The external provider ID (e.g., "603" for TMDB, "tt0133093" for IMDB).</param>
    /// <param name="providerName">The provider name ("tmdb" or "imdb").</param>
    /// <returns>A single SearchResultItem if found, null otherwise.</returns>
    public async Task<SearchResultItem?> SearchByProviderId(string providerId, string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerId) || string.IsNullOrWhiteSpace(providerName))
        {
            _logger.LogDebug("Empty provider ID or provider name provided, returning null");
            return null;
        }

        try
        {
            var normalizedProviderName = providerName.ToLowerInvariant();
            
            // Validate provider name
            if (normalizedProviderName != "tmdb" && normalizedProviderName != "imdb")
            {
                _logger.LogWarning("Unsupported provider name: {ProviderName}. Only 'tmdb' and 'imdb' are supported.", providerName);
                return null;
            }

            _logger.LogDebug("Searching library for {ProviderName} ID: {ProviderId}", providerName, providerId);

            // Query Jellyfin library for movies and episodes with the specified provider ID
            // Use the provider name with proper casing for Jellyfin's ProviderIds dictionary
            var jellyfinProviderKey = normalizedProviderName == "tmdb" ? "Tmdb" : "Imdb";

            var queryResult = _libraryManager.GetItemsResult(new InternalItemsQuery
            {
                HasAnyProviderId = new Dictionary<string, string>
                {
                    { jellyfinProviderKey, providerId }
                },
                IncludeItemTypes = new[] { Jellyfin.Data.Enums.BaseItemKind.Movie, Jellyfin.Data.Enums.BaseItemKind.Episode },
                Recursive = true,
                Limit = 1 // We only need one result
            });

            var items = queryResult.Items;

            if (items.Count == 0)
            {
                _logger.LogDebug("No item found for {ProviderName} ID: {ProviderId}", providerName, providerId);
                return null;
            }

            var item = items[0];
            _logger.LogDebug("Found item '{ItemName}' (ID: {ItemId}) for {ProviderName} ID: {ProviderId}", 
                item.Name, item.Id, providerName, providerId);

            // Convert to SearchResultItem with enriched data
            var searchResultItem = new SearchResultItem
            {
                Id = item.Id,
                Title = item.Name ?? string.Empty,
                Year = item.ProductionYear,
                Type = item switch
                {
                    Movie => "Movie",
                    Episode => "Episode",
                    _ => item.GetType().Name
                },
                ProviderIds = ExtractProviderIds(item)
            };

            // Add episode-specific information
            if (item is Episode episode)
            {
                searchResultItem.SeasonNumber = episode.ParentIndexNumber;
                searchResultItem.SeriesName = episode.SeriesName ?? string.Empty;
            }

            _logger.LogInformation("Successfully found item for {ProviderName} ID: {ProviderId}", providerName, providerId);
            return searchResultItem;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching library for {ProviderName} ID: {ProviderId}. Message: {Message}", 
                providerName, providerId, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Extracts TMDB and IMDB provider IDs from a Jellyfin BaseItem.
    /// </summary>
    /// <param name="item">The Jellyfin library item.</param>
    /// <returns>A dictionary containing available provider IDs with keys "tmdb" and/or "imdb".</returns>
    private Dictionary<string, string> ExtractProviderIds(BaseItem item)
    {
        var providerIds = new Dictionary<string, string>();

        if (item?.ProviderIds == null || item.ProviderIds.Count == 0)
        {
            return providerIds;
        }

        // Extract TMDB ID
        if (item.ProviderIds.TryGetValue("Tmdb", out var tmdbId) && !string.IsNullOrWhiteSpace(tmdbId))
        {
            providerIds["tmdb"] = tmdbId;
        }

        // Extract IMDB ID
        if (item.ProviderIds.TryGetValue("Imdb", out var imdbId) && !string.IsNullOrWhiteSpace(imdbId))
        {
            providerIds["imdb"] = imdbId;
        }

        return providerIds;
    }
}
