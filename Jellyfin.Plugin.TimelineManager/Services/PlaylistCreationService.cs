using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Data.Entities;
using Jellyfin.Plugin.TimelineManager.Models;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TimelineManager.Services;

/// <summary>
/// Service for creating and managing chronological playlists from timeline configurations.
/// </summary>
public class PlaylistCreationService
{
    private readonly ILogger<PlaylistCreationService> _logger;
    private readonly MediaBrowser.Controller.Playlists.IPlaylistManager _playlistManager;
    private readonly ILibraryManager _libraryManager;
    private readonly string _configurationPath;
    private readonly System.Net.Http.IHttpClientFactory? _httpClientFactory;
    private readonly string? _apiKey;
    private readonly Guid? _userId;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaylistCreationService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="playlistManager">The Jellyfin playlist manager.</param>
    /// <param name="libraryManager">The Jellyfin library manager.</param>
    /// <param name="configurationPath">The path to the configuration file.</param>
    /// <param name="httpClientFactory">Optional HTTP client factory for API calls.</param>
    /// <param name="apiKey">Optional Jellyfin API key for authenticated operations.</param>
    /// <param name="userId">Optional user ID for playlist operations.</param>
    public PlaylistCreationService(
        ILogger<PlaylistCreationService> logger,
        MediaBrowser.Controller.Playlists.IPlaylistManager playlistManager,
        ILibraryManager libraryManager,
        string configurationPath = "/config/timeline_manager_config.json",
        System.Net.Http.IHttpClientFactory? httpClientFactory = null,
        string? apiKey = null,
        Guid? userId = null)
    {
        _logger = logger;
        _playlistManager = playlistManager;
        _libraryManager = libraryManager;
        _configurationPath = configurationPath;
        _httpClientFactory = httpClientFactory;
        _apiKey = apiKey;
        _userId = userId;
    }

    /// <summary>
    /// Creates or updates playlists based on the timeline configuration.
    /// </summary>
    /// <returns>A response containing the results of the playlist creation operation.</returns>
    public async Task<PlaylistCreationResponse> CreatePlaylistsAsync()
    {
        var response = new PlaylistCreationResponse
        {
            Success = false,
            Message = "Playlist creation failed",
            Playlists = new List<PlaylistResult>(),
            Errors = new List<string>()
        };

        try
        {
            _logger.LogInformation("Starting playlist creation process");

            // Load configuration
            var configLogger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<ConfigurationService>();
            var configService = new ConfigurationService(configLogger, _configurationPath);
            var configuration = await configService.LoadConfigurationAsync();

            if (configuration == null)
            {
                response.Errors.Add("Failed to load configuration file");
                response.Message = "Configuration file could not be loaded";
                _logger.LogError("Configuration loading failed");
                return response;
            }

            // Validate configuration
            var validationResult = configService.ValidateConfiguration(configuration);
            if (!validationResult.IsValid)
            {
                response.Errors.AddRange(validationResult.Errors);
                response.Message = "Configuration validation failed";
                _logger.LogError("Configuration validation failed with {ErrorCount} errors", validationResult.Errors.Count);
                return response;
            }

            // Build lookup tables
            var lookupLogger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<ContentLookupService>();
            var lookupService = new ContentLookupService(lookupLogger, _libraryManager);
            lookupService.BuildLookupTables();

            _logger.LogInformation("Processing {UniverseCount} universe(s)", configuration.Universes.Count);

            // Process each universe
            var successCount = 0;
            var failureCount = 0;

            foreach (var universe in configuration.Universes)
            {
                try
                {
                    var playlistResult = await CreateOrUpdatePlaylistAsync(universe, lookupService);
                    response.Playlists.Add(playlistResult);
                    successCount++;
                    _logger.LogInformation("Successfully {Action} playlist '{PlaylistName}' with {ItemCount} items",
                        playlistResult.Action, playlistResult.Name, playlistResult.ItemsAdded);
                }
                catch (Exception ex)
                {
                    failureCount++;
                    var errorMessage = $"Failed to create playlist '{universe.Name}': {ex.Message}";
                    response.Errors.Add(errorMessage);
                    _logger.LogError(ex, "Error creating playlist '{PlaylistName}'", universe.Name);
                }
            }

            // Set response status
            response.Success = successCount > 0;
            
            if (successCount == configuration.Universes.Count)
            {
                response.Message = $"Created {successCount} playlist(s) successfully";
            }
            else if (successCount > 0)
            {
                response.Message = $"Created {successCount} of {configuration.Universes.Count} playlist(s). {failureCount} failed.";
            }
            else
            {
                response.Message = "Failed to create any playlists";
            }

            _logger.LogInformation("Playlist creation completed. Success: {SuccessCount}, Failed: {FailureCount}",
                successCount, failureCount);

            return response;
        }
        catch (Exception ex)
        {
            response.Errors.Add($"Unexpected error: {ex.Message}");
            response.Message = "Playlist creation failed with unexpected error";
            _logger.LogError(ex, "Unexpected error during playlist creation");
            return response;
        }
    }

    /// <summary>
    /// Creates or updates a single playlist for a universe.
    /// </summary>
    /// <param name="universe">The universe configuration.</param>
    /// <param name="lookupService">The content lookup service.</param>
    /// <returns>The result of the playlist creation or update.</returns>
    private async Task<PlaylistResult> CreateOrUpdatePlaylistAsync(Universe universe, ContentLookupService lookupService)
    {
        _logger.LogInformation("Processing playlist '{PlaylistName}'", universe.Name);

        // Find items in library
        var (foundItemIds, missingItems) = FindPlaylistItems(universe, lookupService);

        _logger.LogInformation("Found {FoundCount}/{TotalCount} items for playlist '{PlaylistName}'",
            foundItemIds.Count, universe.Items.Count, universe.Name);

        // Check if playlist already exists
        var existingPlaylist = await FindExistingPlaylistAsync(universe.Name);
        var action = existingPlaylist != null ? "updated" : "created";

        if (existingPlaylist != null)
        {
            _logger.LogWarning("Playlist '{PlaylistName}' already exists with ID {PlaylistId}. " +
                "Manual deletion required before creating new playlist.", 
                universe.Name, existingPlaylist.Id);
            
            // For now, we'll skip playlists that already exist
            // In a future version, we can implement proper update logic
            return new PlaylistResult
            {
                Name = universe.Name,
                Action = "skipped",
                ItemsAdded = 0,
                ItemsMissing = missingItems.Count,
                MissingItems = new List<string> { "Playlist already exists. Delete manually and try again." }
            };
        }

        // Create new playlist
        _logger.LogInformation("Creating playlist '{PlaylistName}' with {ItemCount} items", universe.Name, foundItemIds.Count);
        
        try
        {
            // Use Jellyfin's REST API to create the playlist
            await CreatePlaylistViaApi(universe.Name, foundItemIds);
            
            _logger.LogInformation("Successfully created playlist '{PlaylistName}'", universe.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create playlist '{PlaylistName}'", universe.Name);
            throw;
        }

        return new PlaylistResult
        {
            Name = universe.Name,
            Action = action,
            ItemsAdded = foundItemIds.Count,
            ItemsMissing = missingItems.Count,
            MissingItems = missingItems
        };
    }

    /// <summary>
    /// Creates a playlist using Jellyfin's file system structure and then adds items via API.
    /// </summary>
    /// <param name="name">The playlist name.</param>
    /// <param name="itemIds">The list of item IDs.</param>
    private async Task CreatePlaylistViaApi(string name, List<Guid> itemIds)
    {
        // Step 1: Create playlist folder structure (this creates the playlist entity)
        var playlistsPath = "/config/data/playlists";
        var playlistFolder = System.IO.Path.Combine(playlistsPath, name);
        
        if (!System.IO.Directory.Exists(playlistsPath))
        {
            System.IO.Directory.CreateDirectory(playlistsPath);
        }
        
        if (!System.IO.Directory.Exists(playlistFolder))
        {
            System.IO.Directory.CreateDirectory(playlistFolder);
        }

        // Create playlist.xml file
        var playlistXml = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<Item>
  <Added>{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.fffffffZ}</Added>
  <LockData>false</LockData>
  <LocalTitle>{System.Security.SecurityElement.Escape(name)}</LocalTitle>
  <RunTimeTicks>0</RunTimeTicks>
  <Shares>
    <Share>
      <UserId>00000000000000000000000000000000</UserId>
      <CanEdit>true</CanEdit>
    </Share>
  </Shares>
  <PlaylistMediaType>Video</PlaylistMediaType>
  <OwnerUserId>00000000000000000000000000000000</OwnerUserId>
  <OpenAccess>true</OpenAccess>
</Item>";

        var playlistXmlPath = System.IO.Path.Combine(playlistFolder, "playlist.xml");
        await System.IO.File.WriteAllTextAsync(playlistXmlPath, playlistXml);

        _logger.LogInformation("Playlist folder created. Triggering library scan and adding items...");

        // Step 2: Trigger library scan to discover the playlist
        await TriggerLibraryScan();
        
        // Wait a bit for the scan to complete
        _logger.LogInformation("Waiting for library scan to complete...");
        await Task.Delay(5000);

        // Step 3: Find the playlist ID
        _logger.LogInformation("Looking for playlist '{PlaylistName}'...", name);
        var playlistId = await FindPlaylistIdByName(name);
        
        if (playlistId == null)
        {
            _logger.LogWarning("Playlist '{PlaylistName}' not found after library scan. Items not added.", name);
            return;
        }

        _logger.LogInformation("Found playlist '{PlaylistName}' with ID {PlaylistId}. Adding {ItemCount} items...", 
            name, playlistId, itemIds.Count);

        // Step 4: Add items to the playlist
        await AddItemsToPlaylist(playlistId.Value, itemIds);
        
        _logger.LogInformation("Successfully added {ItemCount} items to playlist '{PlaylistName}'", itemIds.Count, name);
    }

    /// <summary>
    /// Triggers a Jellyfin library scan.
    /// </summary>
    private async Task TriggerLibraryScan()
    {
        try
        {
            using var httpClient = new System.Net.Http.HttpClient();
            httpClient.BaseAddress = new Uri("http://localhost:8096");
            httpClient.Timeout = TimeSpan.FromSeconds(10);

            var response = await httpClient.PostAsync("/Library/Refresh", null);
            _logger.LogDebug("Library scan triggered. Status: {StatusCode}", response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to trigger library scan via API. Playlist may need manual refresh.");
        }
    }

    /// <summary>
    /// Finds a playlist ID by name using the library manager.
    /// </summary>
    private async Task<Guid?> FindPlaylistIdByName(string name)
    {
        try
        {
            // Use the existing FindExistingPlaylistAsync method
            var playlist = await FindExistingPlaylistAsync(name);
            return playlist?.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding playlist '{PlaylistName}'", name);
            return null;
        }
    }

    /// <summary>
    /// Adds items to a playlist using the IPlaylistManager directly.
    /// </summary>
    private async Task AddItemsToPlaylist(Guid playlistId, List<Guid> itemIds)
    {
        _logger.LogInformation("=== AddItemsToPlaylist START ===");
        _logger.LogInformation("Playlist ID: {PlaylistId}", playlistId);
        _logger.LogInformation("Item Count: {ItemCount}", itemIds.Count);
        _logger.LogInformation("User ID: {UserId}", _userId);

        if (!_userId.HasValue || _userId.Value == Guid.Empty)
        {
            _logger.LogError("CRITICAL: No valid user ID provided. Items cannot be added to playlist.");
            throw new InvalidOperationException("A valid user ID is required to add items to playlists.");
        }

        try
        {
            _logger.LogInformation("Adding {ItemCount} items to playlist {PlaylistId} using IPlaylistManager", 
                itemIds.Count, playlistId);

            // Use the playlist manager directly instead of HTTP API
            await _playlistManager.AddItemToPlaylistAsync(playlistId, itemIds, _userId.Value);
            
            _logger.LogInformation("=== AddItemsToPlaylist COMPLETE ===");
            _logger.LogInformation("Successfully added {ItemCount} items to playlist", itemIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FATAL ERROR in AddItemsToPlaylist for playlist {PlaylistId}", playlistId);
            throw;
        }
    }

    /// <summary>
    /// Finds all items for a universe in the library.
    /// </summary>
    /// <param name="universe">The universe configuration.</param>
    /// <param name="lookupService">The content lookup service.</param>
    /// <returns>A tuple containing found item IDs and missing item descriptions.</returns>
    private (List<Guid> foundItemIds, List<string> missingItems) FindPlaylistItems(
        Universe universe,
        ContentLookupService lookupService)
    {
        var foundItemIds = new List<Guid>();
        var missingItems = new List<string>();

        foreach (var item in universe.Items)
        {
            var itemId = lookupService.FindItemByProviderId(item.ProviderId, item.ProviderName, item.Type);

            if (itemId.HasValue)
            {
                foundItemIds.Add(itemId.Value);
                _logger.LogDebug("Found item: {Provider}:{ProviderId} ({Type})",
                    item.ProviderName, item.ProviderId, item.Type);
            }
            else
            {
                // For missing items, create a descriptive string
                // In a future enhancement, we could fetch the name from TMDB here
                var seasonSuffix = item.Season.HasValue ? $" S{item.Season.Value}" : "";
                var missingDescription = $"{item.Type} - {item.ProviderName}:{item.ProviderId}{seasonSuffix}";
                missingItems.Add(missingDescription);
                
                _logger.LogDebug("Item not found: {Provider}:{ProviderId} ({Type})",
                    item.ProviderName, item.ProviderId, item.Type);
            }
        }

        return (foundItemIds, missingItems);
    }

    /// <summary>
    /// Finds an existing playlist by name.
    /// </summary>
    /// <param name="playlistName">The name of the playlist to find.</param>
    /// <returns>The existing playlist if found, null otherwise.</returns>
    private async Task<MediaBrowser.Controller.Playlists.Playlist?> FindExistingPlaylistAsync(string playlistName)
    {
        try
        {
            // Try to find playlist by searching the file system directly
            // since GetPlaylists(Guid.Empty) doesn't work
            var playlistsPath = "/config/data/playlists";
            var playlistFolder = System.IO.Path.Combine(playlistsPath, playlistName);
            
            if (System.IO.Directory.Exists(playlistFolder))
            {
                // Playlist folder exists, try to find it in the library
                // Use FindByPath which should work without a user ID
                var playlistXmlPath = System.IO.Path.Combine(playlistFolder, "playlist.xml");
                if (System.IO.File.Exists(playlistXmlPath))
                {
                    var item = _libraryManager.FindByPath(playlistFolder, true);
                    if (item is MediaBrowser.Controller.Playlists.Playlist playlist)
                    {
                        _logger.LogDebug("Found existing playlist '{PlaylistName}' with ID {PlaylistId}",
                            playlistName, playlist.Id);
                        return playlist;
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching for existing playlist '{PlaylistName}'", playlistName);
            return null;
        }
    }
}
