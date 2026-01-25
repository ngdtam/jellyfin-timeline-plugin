using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Jellyfin.Plugin.TimelineManager.Models;
using Jellyfin.Plugin.TimelineManager.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TimelineManager.Api;

/// <summary>
/// API controller for managing individual universe files.
/// </summary>
[ApiController]
[Route("Timeline/Universes")]
[Authorize(Policy = "RequiresElevation")]
public class UniverseManagementController : ControllerBase
{
    private readonly ILogger<UniverseManagementController> _logger;
    private readonly UniverseManagementService _universeManagementService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UniverseManagementController"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="universeManagementService">Universe management service.</param>
    public UniverseManagementController(
        ILogger<UniverseManagementController> logger,
        UniverseManagementService universeManagementService)
    {
        _logger = logger;
        _universeManagementService = universeManagementService;
    }

    /// <summary>
    /// Gets metadata for all available universe files.
    /// </summary>
    /// <returns>A list of universe metadata objects.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(List<UniverseMetadata>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<UniverseMetadata>>> GetAllUniverses()
    {
        try
        {
            _logger.LogInformation("Getting all universe files");
            var universes = await _universeManagementService.GetAllUniversesAsync();
            _logger.LogInformation("Found {Count} universe files", universes.Count);
            return Ok(universes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all universes");
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { error = "Failed to retrieve universe list", details = ex.Message });
        }
    }

    /// <summary>
    /// Gets the full content of a specific universe file.
    /// </summary>
    /// <param name="filename">The filename of the universe (e.g., "mcu.json").</param>
    /// <returns>The universe object.</returns>
    [HttpGet("{filename}")]
    [ProducesResponseType(typeof(Universe), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<Universe>> GetUniverse(string filename)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                _logger.LogWarning("GetUniverse called with empty filename");
                return BadRequest(new { error = "Filename is required" });
            }

            // Validate filename to prevent directory traversal
            if (filename.Contains("..") || filename.Contains("/") || filename.Contains("\\"))
            {
                _logger.LogWarning("GetUniverse called with invalid filename: {Filename}", filename);
                return BadRequest(new { error = "Invalid filename" });
            }

            _logger.LogInformation("Getting universe file: {Filename}", filename);
            var universe = await _universeManagementService.GetUniverseAsync(filename);

            if (universe == null)
            {
                _logger.LogWarning("Universe file not found: {Filename}", filename);
                return NotFound(new { error = $"Universe file '{filename}' not found" });
            }

            _logger.LogInformation("Successfully retrieved universe '{UniverseName}' from {Filename}", 
                universe.Name, filename);
            return Ok(universe);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting universe file {Filename}", filename);
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { error = "Failed to retrieve universe", details = ex.Message });
        }
    }

    /// <summary>
    /// Saves or updates a universe file.
    /// </summary>
    /// <param name="filename">The filename to save to (e.g., "mcu.json").</param>
    /// <param name="universe">The universe object to save.</param>
    /// <returns>The result of the save operation.</returns>
    [HttpPost("{filename}")]
    [ProducesResponseType(typeof(SaveUniverseResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SaveUniverseResult>> SaveUniverse(string filename, [FromBody] Universe universe)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                _logger.LogWarning("SaveUniverse called with empty filename");
                return BadRequest(new SaveUniverseResult 
                { 
                    Success = false, 
                    Errors = new List<string> { "Filename is required" } 
                });
            }

            // Validate filename to prevent directory traversal
            if (filename.Contains("..") || filename.Contains("/") || filename.Contains("\\"))
            {
                _logger.LogWarning("SaveUniverse called with invalid filename: {Filename}", filename);
                return BadRequest(new SaveUniverseResult 
                { 
                    Success = false, 
                    Errors = new List<string> { "Invalid filename" } 
                });
            }

            if (universe == null)
            {
                _logger.LogWarning("SaveUniverse called with null universe");
                return BadRequest(new SaveUniverseResult 
                { 
                    Success = false, 
                    Errors = new List<string> { "Universe data is required" } 
                });
            }

            _logger.LogInformation("Saving universe '{UniverseName}' to {Filename}", universe.Name, filename);
            var result = await _universeManagementService.SaveUniverseAsync(filename, universe);

            if (result.Success)
            {
                _logger.LogInformation("Successfully saved universe '{UniverseName}' to {Filename}", 
                    universe.Name, filename);
                return Ok(result);
            }
            else
            {
                _logger.LogWarning("Failed to save universe '{UniverseName}': {Errors}", 
                    universe.Name, string.Join(", ", result.Errors));
                return BadRequest(result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving universe file {Filename}", filename);
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new SaveUniverseResult 
                { 
                    Success = false, 
                    Errors = new List<string> { $"Failed to save universe: {ex.Message}" } 
                });
        }
    }

    /// <summary>
    /// Deletes a universe file.
    /// </summary>
    /// <param name="filename">The filename to delete (e.g., "mcu.json").</param>
    /// <returns>A result indicating success or failure.</returns>
    [HttpDelete("{filename}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> DeleteUniverse(string filename)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                _logger.LogWarning("DeleteUniverse called with empty filename");
                return BadRequest(new { error = "Filename is required" });
            }

            // Validate filename to prevent directory traversal
            if (filename.Contains("..") || filename.Contains("/") || filename.Contains("\\"))
            {
                _logger.LogWarning("DeleteUniverse called with invalid filename: {Filename}", filename);
                return BadRequest(new { error = "Invalid filename" });
            }

            _logger.LogInformation("Deleting universe file: {Filename}", filename);
            var success = await _universeManagementService.DeleteUniverseAsync(filename);

            if (success)
            {
                _logger.LogInformation("Successfully deleted universe file: {Filename}", filename);
                return Ok(new { success = true, message = $"Universe file '{filename}' deleted successfully" });
            }
            else
            {
                _logger.LogWarning("Failed to delete universe file: {Filename}", filename);
                return NotFound(new { error = $"Universe file '{filename}' not found or could not be deleted" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting universe file {Filename}", filename);
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { error = "Failed to delete universe", details = ex.Message });
        }
    }
}
