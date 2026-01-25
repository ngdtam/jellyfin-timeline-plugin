using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Plugin.TimelineManager.Models;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TimelineManager.Services;

/// <summary>
/// Service for searching TMDB API for movies and TV shows.
/// </summary>
public class TmdbSearchService
{
    private const string TmdbBaseUrl = "https://api.themoviedb.org/3";
    
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TmdbSearchService> _logger;
    private readonly IApplicationPaths _applicationPaths;

    /// <summary>
    /// Initializes a new instance of the <see cref="TmdbSearchService"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="applicationPaths">The application paths.</param>
    public TmdbSearchService(
        IHttpClientFactory httpClientFactory, 
        ILogger<TmdbSearchService> logger,
        IApplicationPaths applicationPaths)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _applicationPaths = applicationPaths;
    }

    /// <summary>
    /// Gets the TMDB API key from plugin configuration.
    /// </summary>
    /// <returns>The TMDB API key, or null if not configured.</returns>
    private string? GetTmdbApiKey()
    {
        try
        {
            // Read the plugin configuration XML file
            var configPath = Path.Combine(_applicationPaths.PluginConfigurationsPath, "Jellyfin.Plugin.TimelineManager.xml");
            
            if (!File.Exists(configPath))
            {
                _logger.LogWarning("Plugin configuration file not found at {ConfigPath}", configPath);
                return null;
            }

            var xmlContent = File.ReadAllText(configPath);
            
            // Simple XML parsing to extract TmdbApiKey
            var startTag = "<TmdbApiKey>";
            var endTag = "</TmdbApiKey>";
            var startIndex = xmlContent.IndexOf(startTag);
            
            if (startIndex == -1)
            {
                _logger.LogDebug("TmdbApiKey not found in configuration");
                return null;
            }

            startIndex += startTag.Length;
            var endIndex = xmlContent.IndexOf(endTag, startIndex);
            
            if (endIndex == -1)
            {
                _logger.LogWarning("Malformed TmdbApiKey in configuration");
                return null;
            }

            var apiKey = xmlContent.Substring(startIndex, endIndex - startIndex).Trim();
            return string.IsNullOrWhiteSpace(apiKey) ? null : apiKey;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading TMDB API key from configuration");
            return null;
        }
    }

    /// <summary>
    /// Searches TMDB for movies matching the query.
    /// </summary>
    /// <param name="query">The search query string.</param>
    /// <param name="limit">Maximum number of results to return (default 20).</param>
    /// <returns>List of SearchResultItem objects.</returns>
    public async Task<List<SearchResultItem>> SearchMovies(string query, int limit = 20)
    {
        var results = new List<SearchResultItem>();

        if (string.IsNullOrWhiteSpace(query))
        {
            _logger.LogDebug("Empty search query provided for movies, returning empty results");
            return results;
        }

        var apiKey = GetTmdbApiKey();
        if (apiKey == null)
        {
            _logger.LogWarning("TMDB API key not configured. Please configure it in plugin settings.");
            return results;
        }

        try
        {
            _logger.LogDebug("Searching TMDB for movies: {Query} with limit: {Limit}", query, limit);

            var url = $"{TmdbBaseUrl}/search/movie?api_key={apiKey}&query={Uri.EscapeDataString(query)}";
            
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            var response = await httpClient.GetAsync(url).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var tmdbResponse = JsonSerializer.Deserialize<TmdbSearchResponse<TmdbMovieSearchResult>>(content);

            if (tmdbResponse?.Results == null)
            {
                _logger.LogWarning("TMDB movie search returned null response for query: {Query}", query);
                return results;
            }

            _logger.LogDebug("Found {Count} movie results from TMDB for query: {Query}", tmdbResponse.Results.Count, query);

            // Convert TMDB results to SearchResultItem format
            var itemsToTake = Math.Min(limit, tmdbResponse.Results.Count);
            for (int i = 0; i < itemsToTake; i++)
            {
                var movie = tmdbResponse.Results[i];
                results.Add(ConvertMovieToSearchResultItem(movie));
            }

            _logger.LogInformation("Successfully returned {ResultCount} movie search results from TMDB for query: {Query}", results.Count, query);
            return results;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error searching TMDB for movies with query: {Query}. Message: {Message}", query, ex.Message);
            return results;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Timeout searching TMDB for movies with query: {Query}. Message: {Message}", query, ex.Message);
            return results;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parsing error for TMDB movie search with query: {Query}. Message: {Message}", query, ex.Message);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error searching TMDB for movies with query: {Query}. Message: {Message}", query, ex.Message);
            return results;
        }
    }

    /// <summary>
    /// Searches TMDB for TV shows matching the query.
    /// </summary>
    /// <param name="query">The search query string.</param>
    /// <param name="limit">Maximum number of results to return (default 20).</param>
    /// <returns>List of SearchResultItem objects.</returns>
    public async Task<List<SearchResultItem>> SearchTvShows(string query, int limit = 20)
    {
        var results = new List<SearchResultItem>();

        if (string.IsNullOrWhiteSpace(query))
        {
            _logger.LogDebug("Empty search query provided for TV shows, returning empty results");
            return results;
        }

        var apiKey = GetTmdbApiKey();
        if (apiKey == null)
        {
            _logger.LogWarning("TMDB API key not configured. Please configure it in plugin settings.");
            return results;
        }

        try
        {
            _logger.LogDebug("Searching TMDB for TV shows: {Query} with limit: {Limit}", query, limit);

            var url = $"{TmdbBaseUrl}/search/tv?api_key={apiKey}&query={Uri.EscapeDataString(query)}";
            
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            var response = await httpClient.GetAsync(url).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var tmdbResponse = JsonSerializer.Deserialize<TmdbSearchResponse<TmdbTvSearchResult>>(content);

            if (tmdbResponse?.Results == null)
            {
                _logger.LogWarning("TMDB TV search returned null response for query: {Query}", query);
                return results;
            }

            _logger.LogDebug("Found {Count} TV show results from TMDB for query: {Query}", tmdbResponse.Results.Count, query);

            // Convert TMDB results to SearchResultItem format
            var itemsToTake = Math.Min(limit, tmdbResponse.Results.Count);
            for (int i = 0; i < itemsToTake; i++)
            {
                var tvShow = tmdbResponse.Results[i];
                results.Add(ConvertTvShowToSearchResultItem(tvShow));
            }

            _logger.LogInformation("Successfully returned {ResultCount} TV show search results from TMDB for query: {Query}", results.Count, query);
            return results;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error searching TMDB for TV shows with query: {Query}. Message: {Message}", query, ex.Message);
            return results;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Timeout searching TMDB for TV shows with query: {Query}. Message: {Message}", query, ex.Message);
            return results;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parsing error for TMDB TV search with query: {Query}. Message: {Message}", query, ex.Message);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error searching TMDB for TV shows with query: {Query}. Message: {Message}", query, ex.Message);
            return results;
        }
    }

    /// <summary>
    /// Parses year from a date string in format "YYYY-MM-DD".
    /// </summary>
    /// <param name="dateString">The date string to parse.</param>
    /// <returns>The year as an integer, or null if parsing fails.</returns>
    private static int? ParseYear(string dateString)
    {
        if (string.IsNullOrWhiteSpace(dateString))
        {
            return null;
        }

        if (DateTime.TryParseExact(dateString, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date.Year;
        }

        // Try to extract just the year if full date parsing fails
        if (dateString.Length >= 4 && int.TryParse(dateString.Substring(0, 4), out var year))
        {
            return year;
        }

        return null;
    }

    /// <summary>
    /// Converts a TMDB movie result to SearchResultItem format.
    /// </summary>
    /// <param name="movie">The TMDB movie search result.</param>
    /// <returns>A SearchResultItem object.</returns>
    private static SearchResultItem ConvertMovieToSearchResultItem(TmdbMovieSearchResult movie)
    {
        return new SearchResultItem
        {
            Id = Guid.Empty, // No Jellyfin ID for TMDB items
            Title = movie.Title,
            Year = ParseYear(movie.ReleaseDate),
            Type = "Movie",
            ProviderIds = new Dictionary<string, string> { { "tmdb", movie.Id.ToString() } },
            SeasonNumber = null,
            SeriesName = string.Empty
        };
    }

    /// <summary>
    /// Converts a TMDB TV show result to SearchResultItem format.
    /// </summary>
    /// <param name="tvShow">The TMDB TV show search result.</param>
    /// <returns>A SearchResultItem object.</returns>
    private static SearchResultItem ConvertTvShowToSearchResultItem(TmdbTvSearchResult tvShow)
    {
        return new SearchResultItem
        {
            Id = Guid.Empty, // No Jellyfin ID for TMDB items
            Title = tvShow.Name,
            Year = ParseYear(tvShow.FirstAirDate),
            Type = "Series", // Will be converted to "episode" when creating TimelineItem
            ProviderIds = new Dictionary<string, string> { { "tmdb", tvShow.Id.ToString() } },
            SeasonNumber = null, // User can specify season when adding to playlist
            SeriesName = tvShow.Name
        };
    }
}
