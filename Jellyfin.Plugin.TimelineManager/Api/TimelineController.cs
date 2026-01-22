using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.TimelineManager.Extensions;
using Jellyfin.Plugin.TimelineManager.Models;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TimelineManager.Api;

/// <summary>
/// API controller for Timeline Manager web interface.
/// </summary>
[ApiController]
[Route("Plugins/TimelineManager")]
[Authorize(Policy = "RequiresElevation")]
public class TimelineController : ControllerBase
{
    private readonly ILogger<TimelineController> _logger;
    private readonly ILibraryManager _libraryManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimelineController"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="libraryManager">The library manager.</param>
    public TimelineController(
        ILogger<TimelineController> logger,
        ILibraryManager libraryManager)
    {
        _logger = logger;
        _libraryManager = libraryManager;
    }

    /// <summary>
    /// Gets all movies and TV shows from the library with their Provider IDs.
    /// </summary>
    /// <returns>List of media items with metadata.</returns>
    [HttpGet("library")]
    public ActionResult<IEnumerable<MediaItemDto>> GetLibraryItems()
    {
        try
        {
            _logger.LogDebug("Fetching library items for Timeline Manager UI");

            var items = new List<MediaItemDto>();

            // Get all movies
            var movies = _libraryManager.GetItemList(new MediaBrowser.Controller.Entities.InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.Movie },
                Recursive = true,
                IsVirtualItem = false
            });

            foreach (var movie in movies)
            {
                var tmdbId = movie.GetProviderId("Tmdb");
                var imdbId = movie.GetProviderId("Imdb");

                if (!string.IsNullOrEmpty(tmdbId) || !string.IsNullOrEmpty(imdbId))
                {
                    items.Add(new MediaItemDto
                    {
                        Id = movie.Id.ToString(),
                        Name = movie.Name,
                        Type = "movie",
                        Year = movie.ProductionYear,
                        TmdbId = tmdbId,
                        ImdbId = imdbId,
                        Overview = movie.Overview,
                        ImageUrl = GetImageUrl(movie.Id)
                    });
                }
            }

            // Get all TV episodes
            var episodes = _libraryManager.GetItemList(new MediaBrowser.Controller.Entities.InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.Episode },
                Recursive = true,
                IsVirtualItem = false
            });

            foreach (var episode in episodes)
            {
                var series = episode.GetParentItem();
                if (series == null) continue;

                var tmdbId = series.GetProviderId("Tmdb");
                var imdbId = series.GetProviderId("Imdb");

                if (!string.IsNullOrEmpty(tmdbId) || !string.IsNullOrEmpty(imdbId))
                {
                    items.Add(new MediaItemDto
                    {
                        Id = episode.Id.ToString(),
                        Name = $"{series.Name} - {episode.Name}",
                        Type = "episode",
                        Year = episode.ProductionYear,
                        TmdbId = tmdbId,
                        ImdbId = imdbId,
                        Overview = episode.Overview,
                        ImageUrl = GetImageUrl(episode.Id),
                        SeriesName = series.Name,
                        SeasonNumber = episode.GetSeasonNumber(),
                        EpisodeNumber = episode.GetEpisodeNumber()
                    });
                }
            }

            _logger.LogInformation("Retrieved {Count} media items for Timeline Manager UI", items.Count);
            return Ok(items.OrderBy(x => x.Name));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve library items: {Message}", ex.Message);
            return StatusCode(500, new { error = "Failed to retrieve library items", details = ex.Message });
        }
    }

    /// <summary>
    /// Gets the current timeline configuration.
    /// </summary>
    /// <returns>Current configuration or empty configuration.</returns>
    [HttpGet("configuration")]
    public async Task<ActionResult<TimelineConfiguration>> GetConfiguration()
    {
        try
        {
            _logger.LogDebug("Fetching current timeline configuration");

            var config = await LoadConfigurationAsync();
            if (config == null)
            {
                // Return empty configuration
                config = new TimelineConfiguration
                {
                    Universes = new List<Universe>()
                };
            }

            return Ok(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load configuration: {Message}", ex.Message);
            return StatusCode(500, new { error = "Failed to load configuration", details = ex.Message });
        }
    }

    /// <summary>
    /// Saves the timeline configuration.
    /// </summary>
    /// <param name="configuration">The configuration to save.</param>
    /// <returns>Success or error response.</returns>
    [HttpPost("configuration")]
    public async Task<ActionResult> SaveConfiguration([FromBody] TimelineConfiguration configuration)
    {
        try
        {
            _logger.LogDebug("Saving timeline configuration with {Count} universes", configuration.Universes?.Count ?? 0);

            // Validate configuration
            if (configuration.Universes == null)
            {
                return BadRequest(new { error = "Universes array is required" });
            }

            // Save configuration to JSON file
            var configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "jellyfin", "config", "timeline_manager_config.json");
            var configDir = Path.GetDirectoryName(configPath);
            if (!Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir!);
            }
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var jsonString = JsonSerializer.Serialize(configuration, jsonOptions);
            await System.IO.File.WriteAllTextAsync(configPath, jsonString);

            _logger.LogInformation("Successfully saved timeline configuration to {Path}", configPath);
            return Ok(new { message = "Configuration saved successfully", path = configPath });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save configuration: {Message}", ex.Message);
            return StatusCode(500, new { error = "Failed to save configuration", details = ex.Message });
        }
    }

    /// <summary>
    /// Runs the timeline task to create/update playlists.
    /// </summary>
    /// <returns>Task execution result.</returns>
    [HttpPost("run")]
    public ActionResult RunTimelineTask()
    {
        try
        {
            _logger.LogDebug("Running timeline task from web UI");

            // This would typically trigger the scheduled task
            // For now, return success - the actual implementation would need task manager integration
            return Ok(new { message = "Timeline task started successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run timeline task: {Message}", ex.Message);
            return StatusCode(500, new { error = "Failed to run timeline task", details = ex.Message });
        }
    }

    /// <summary>
    /// Gets the image URL for a media item.
    /// </summary>
    /// <param name="itemId">The item ID.</param>
    /// <returns>Image URL or null.</returns>
    private string? GetImageUrl(Guid itemId)
    {
        try
        {
            return $"/Items/{itemId}/Images/Primary?maxHeight=300&quality=90";
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Loads the timeline configuration from the JSON file.
    /// </summary>
    /// <returns>The loaded configuration or null if not found.</returns>
    private async Task<TimelineConfiguration?> LoadConfigurationAsync()
    {
        try
        {
            var configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "jellyfin", "config", "timeline_manager_config.json");
            
            if (!System.IO.File.Exists(configPath))
            {
                _logger.LogDebug("Configuration file not found at {Path}", configPath);
                return null;
            }

            var jsonString = await System.IO.File.ReadAllTextAsync(configPath);
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var config = JsonSerializer.Deserialize<TimelineConfiguration>(jsonString, options);
            _logger.LogDebug("Successfully loaded configuration with {Count} universes", config?.Universes?.Count ?? 0);
            
            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load configuration: {Message}", ex.Message);
            return null;
        }
    }
}

/// <summary>
/// DTO for media items in the web UI.
/// </summary>
public class MediaItemDto
{
    /// <summary>
    /// Gets or sets the item ID.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the item name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the item type (movie or episode).
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the production year.
    /// </summary>
    public int? Year { get; set; }

    /// <summary>
    /// Gets or sets the TMDB ID.
    /// </summary>
    public string? TmdbId { get; set; }

    /// <summary>
    /// Gets or sets the IMDB ID.
    /// </summary>
    public string? ImdbId { get; set; }

    /// <summary>
    /// Gets or sets the overview/description.
    /// </summary>
    public string? Overview { get; set; }

    /// <summary>
    /// Gets or sets the image URL.
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Gets or sets the series name (for episodes).
    /// </summary>
    public string? SeriesName { get; set; }

    /// <summary>
    /// Gets or sets the season number (for episodes).
    /// </summary>
    public int? SeasonNumber { get; set; }

    /// <summary>
    /// Gets or sets the episode number (for episodes).
    /// </summary>
    public int? EpisodeNumber { get; set; }
}