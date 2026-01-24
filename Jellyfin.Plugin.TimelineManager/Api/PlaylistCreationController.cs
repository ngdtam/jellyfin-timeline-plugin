using System;
using System.Threading.Tasks;
using Jellyfin.Plugin.TimelineManager.Models;
using Jellyfin.Plugin.TimelineManager.Services;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TimelineManager.Api;

/// <summary>
/// API controller for playlist creation from timeline configurations.
/// </summary>
[ApiController]
[Route("Timeline")]
[Authorize(Policy = "RequiresElevation")]
public class PlaylistCreationController : ControllerBase
{
    private readonly ILogger<PlaylistCreationController> _logger;
    private readonly MediaBrowser.Controller.Playlists.IPlaylistManager _playlistManager;
    private readonly ILibraryManager _libraryManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaylistCreationController"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="playlistManager">Jellyfin playlist manager.</param>
    /// <param name="libraryManager">Jellyfin library manager.</param>
    public PlaylistCreationController(
        ILogger<PlaylistCreationController> logger,
        MediaBrowser.Controller.Playlists.IPlaylistManager playlistManager,
        ILibraryManager libraryManager)
    {
        _logger = logger;
        _playlistManager = playlistManager;
        _libraryManager = libraryManager;
    }

    /// <summary>
    /// Creates or updates playlists based on the timeline configuration file.
    /// </summary>
    /// <param name="userId">Optional user ID. If not provided, will attempt to extract from authentication context.</param>
    /// <returns>The result of the playlist creation operation.</returns>
    [HttpPost("CreatePlaylists")]
    [ProducesResponseType(typeof(PlaylistCreationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PlaylistCreationResponse>> CreatePlaylists([FromQuery] Guid? userId = null)
    {
        try
        {
            _logger.LogInformation("=== [Timeline API] CreatePlaylists endpoint called ===");

            // Try to get user ID from query parameter, or use a default
            Guid effectiveUserId;
            if (userId.HasValue && userId.Value != Guid.Empty)
            {
                effectiveUserId = userId.Value;
                _logger.LogInformation("[Timeline API] Using user ID from query parameter: {UserId}", effectiveUserId);
            }
            else
            {
                // Try to get from X-Emby-UserId header (set by Jellyfin when authenticated)
                if (Request.Headers.TryGetValue("X-Emby-UserId", out var userIdHeader) && 
                    Guid.TryParse(userIdHeader.FirstOrDefault(), out var parsedUserId))
                {
                    effectiveUserId = parsedUserId;
                    _logger.LogInformation("[Timeline API] Using user ID from X-Emby-UserId header: {UserId}", effectiveUserId);
                }
                else
                {
                    _logger.LogError("[Timeline API] No user ID provided and could not extract from headers");
                    return Ok(new PlaylistCreationResponse
                    {
                        Success = false,
                        Message = "User ID is required. Please provide userId query parameter.",
                        Playlists = new System.Collections.Generic.List<PlaylistResult>(),
                        Errors = new System.Collections.Generic.List<string> { "No user ID provided" }
                    });
                }
            }

            // Get API key from plugin configuration (for future use if needed)
            var config = Plugin.Instance?.Configuration as PluginConfiguration;
            var apiKey = config?.JellyfinApiKey;
            _logger.LogInformation("[Timeline API] API Key configured: {HasApiKey}", !string.IsNullOrEmpty(apiKey));

            // Create logger for the service
            var serviceLogger = LoggerFactory.Create(builder => builder.AddConsole())
                .CreateLogger<PlaylistCreationService>();

            // Create the playlist creation service with user ID
            _logger.LogInformation("[Timeline API] Creating PlaylistCreationService with user ID...");
            var service = new PlaylistCreationService(
                serviceLogger,
                _playlistManager,
                _libraryManager,
                userId: effectiveUserId);

            // Execute playlist creation
            _logger.LogInformation("[Timeline API] Executing CreatePlaylistsAsync...");
            var response = await service.CreatePlaylistsAsync();

            _logger.LogInformation("=== [Timeline API] CreatePlaylists completed ===");
            _logger.LogInformation("[Timeline API] Success: {Success}, Playlists: {Count}",
                response.Success, response.Playlists.Count);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Timeline API] Unexpected error in CreatePlaylists endpoint");
            
            return Ok(new PlaylistCreationResponse
            {
                Success = false,
                Message = "An unexpected error occurred while creating playlists",
                Playlists = new System.Collections.Generic.List<PlaylistResult>(),
                Errors = new System.Collections.Generic.List<string> { ex.Message }
            });
        }
    }
}
