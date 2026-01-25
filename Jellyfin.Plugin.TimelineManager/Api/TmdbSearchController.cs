using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Jellyfin.Plugin.TimelineManager.Models;
using Jellyfin.Plugin.TimelineManager.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TimelineManager.Api;

/// <summary>
/// API controller for TMDB search operations.
/// </summary>
[ApiController]
[Route("Timeline/Search/Tmdb")]
[Authorize(Policy = "RequiresElevation")]
public class TmdbSearchController : ControllerBase
{
    private readonly TmdbSearchService _tmdbSearchService;
    private readonly ILogger<TmdbSearchController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TmdbSearchController"/> class.
    /// </summary>
    /// <param name="tmdbSearchService">The TMDB search service.</param>
    /// <param name="logger">The logger instance.</param>
    public TmdbSearchController(TmdbSearchService tmdbSearchService, ILogger<TmdbSearchController> logger)
    {
        _tmdbSearchService = tmdbSearchService;
        _logger = logger;
    }

    /// <summary>
    /// Searches TMDB for movies matching the query.
    /// </summary>
    /// <param name="query">The search query string.</param>
    /// <param name="limit">Maximum number of results (default 20).</param>
    /// <returns>SearchResultsResponse with movie results.</returns>
    [HttpGet("Movies")]
    [ProducesResponseType(typeof(SearchResultsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SearchResultsResponse>> SearchMovies(
        [FromQuery][Required] string query,
        [FromQuery] int limit = 20)
    {
        try
        {
            // Validate parameters
            if (string.IsNullOrWhiteSpace(query))
            {
                _logger.LogWarning("Empty query parameter provided for TMDB movie search");
                return BadRequest("Query parameter is required");
            }

            if (limit < 1 || limit > 100)
            {
                _logger.LogWarning("Invalid limit parameter provided for TMDB movie search: {Limit}", limit);
                return BadRequest("Limit must be between 1 and 100");
            }

            _logger.LogDebug("TMDB movie search request: query={Query}, limit={Limit}", query, limit);

            // Call service
            var results = await _tmdbSearchService.SearchMovies(query, limit).ConfigureAwait(false);

            // Return response
            var response = new SearchResultsResponse
            {
                Results = results,
                TotalCount = results.Count
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TMDB movie search endpoint for query: {Query}. Message: {Message}", query, ex.Message);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while searching TMDB for movies");
        }
    }

    /// <summary>
    /// Searches TMDB for TV shows matching the query.
    /// </summary>
    /// <param name="query">The search query string.</param>
    /// <param name="limit">Maximum number of results (default 20).</param>
    /// <returns>SearchResultsResponse with TV show results.</returns>
    [HttpGet("Tv")]
    [ProducesResponseType(typeof(SearchResultsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SearchResultsResponse>> SearchTvShows(
        [FromQuery][Required] string query,
        [FromQuery] int limit = 20)
    {
        try
        {
            // Validate parameters
            if (string.IsNullOrWhiteSpace(query))
            {
                _logger.LogWarning("Empty query parameter provided for TMDB TV search");
                return BadRequest("Query parameter is required");
            }

            if (limit < 1 || limit > 100)
            {
                _logger.LogWarning("Invalid limit parameter provided for TMDB TV search: {Limit}", limit);
                return BadRequest("Limit must be between 1 and 100");
            }

            _logger.LogDebug("TMDB TV search request: query={Query}, limit={Limit}", query, limit);

            // Call service
            var results = await _tmdbSearchService.SearchTvShows(query, limit).ConfigureAwait(false);

            // Return response
            var response = new SearchResultsResponse
            {
                Results = results,
                TotalCount = results.Count
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TMDB TV search endpoint for query: {Query}. Message: {Message}", query, ex.Message);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while searching TMDB for TV shows");
        }
    }
}
