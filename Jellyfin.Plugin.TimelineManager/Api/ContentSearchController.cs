using System;
using System.Threading.Tasks;
using Jellyfin.Plugin.TimelineManager.Models;
using Jellyfin.Plugin.TimelineManager.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TimelineManager.Api;

/// <summary>
/// API controller for searching Jellyfin library content.
/// </summary>
[ApiController]
[Route("Timeline/Search")]
[Authorize(Policy = "RequiresElevation")]
public class ContentSearchController : ControllerBase
{
    private readonly ILogger<ContentSearchController> _logger;
    private readonly ContentSearchService _contentSearchService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentSearchController"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="contentSearchService">Content search service.</param>
    public ContentSearchController(
        ILogger<ContentSearchController> logger,
        ContentSearchService contentSearchService)
    {
        _logger = logger;
        _contentSearchService = contentSearchService;
    }

    /// <summary>
    /// Searches the Jellyfin library for movies and episodes matching the specified query.
    /// </summary>
    /// <param name="query">The search query string to match against titles.</param>
    /// <param name="limit">The maximum number of results to return (default 20).</param>
    /// <returns>A SearchResultsResponse containing matching items and total count.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(SearchResultsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SearchResultsResponse>> SearchContent(
        [FromQuery] string query,
        [FromQuery] int limit = 20)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                _logger.LogWarning("SearchContent called with empty query");
                return BadRequest(new { error = "Query parameter is required" });
            }

            if (limit <= 0)
            {
                _logger.LogWarning("SearchContent called with invalid limit: {Limit}", limit);
                return BadRequest(new { error = "Limit must be greater than 0" });
            }

            _logger.LogInformation("Searching content with query: {Query}, limit: {Limit}", query, limit);
            var results = await _contentSearchService.SearchByTitle(query, limit);

            var response = new SearchResultsResponse
            {
                Results = results,
                TotalCount = results.Count
            };

            _logger.LogInformation("Search completed. Found {Count} results for query: {Query}", results.Count, query);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching content with query: {Query}", query);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "Failed to search content", details = ex.Message });
        }
    }
}
