using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Entities;
using Jellyfin.Plugin.TimelineManager.Models;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
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
    private readonly string? _authToken;
    private readonly List<string>? _selectedUniverseFilenames;

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
    /// <param name="authToken">Optional authentication token for HTTP API calls.</param>
    /// <param name="selectedUniverseFilenames">Optional list of universe filenames to process selectively.</param>
    public PlaylistCreationService(
        ILogger<PlaylistCreationService> logger,
        MediaBrowser.Controller.Playlists.IPlaylistManager playlistManager,
        ILibraryManager libraryManager,
        string configurationPath = "/config/timeline_manager_config.json",
        System.Net.Http.IHttpClientFactory? httpClientFactory = null,
        string? apiKey = null,
        Guid? userId = null,
        string? authToken = null,
        List<string>? selectedUniverseFilenames = null)
    {
        _logger = logger;
        _playlistManager = playlistManager;
        _libraryManager = libraryManager;
        _configurationPath = configurationPath;
        _httpClientFactory = httpClientFactory;
        _apiKey = apiKey;
        _userId = userId;
        _authToken = authToken;
        _selectedUniverseFilenames = selectedUniverseFilenames;
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
            var universeManagementLogger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<UniverseManagementService>();
            var universeManagementService = new UniverseManagementService(universeManagementLogger);
            var configService = new ConfigurationService(configLogger, _configurationPath, universeManagementService);

            TimelineConfiguration? configuration;

            // Check if selective universe processing is requested
            if (_selectedUniverseFilenames != null && _selectedUniverseFilenames.Count > 0)
            {
                _logger.LogInformation("Loading {Count} selected universes", _selectedUniverseFilenames.Count);
                configuration = await configService.LoadFromUniverseFilesAsync(_selectedUniverseFilenames);
            }
            else
            {
                // Try to load from multi-file format first, fallback to legacy single-file
                _logger.LogInformation("Attempting to load from multi-file universe format");
                configuration = await configService.LoadAllUniversesAsync();
                
                if (configuration == null || configuration.Universes.Count == 0)
                {
                    _logger.LogInformation("No universes found in multi-file format, falling back to legacy single-file format");
                    configuration = await configService.LoadConfigurationAsync();
                }
            }

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

        // For now, always create a new playlist
        // TODO: In future, implement playlist ID tracking to enable updates
        _logger.LogInformation("Creating new playlist '{PlaylistName}' with {ItemCount} items", 
            universe.Name, foundItemIds.Count);
        
        try
        {
            // Create playlist using the LinkedChildren approach (SmartLists method)
            await CreatePlaylistWithItemsAsync(universe.Name, foundItemIds);
            
            _logger.LogInformation("Successfully created playlist '{PlaylistName}' with {ItemCount} items", 
                universe.Name, foundItemIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create playlist '{PlaylistName}'", universe.Name);
            throw;
        }

        return new PlaylistResult
        {
            Name = universe.Name,
            Action = "created",
            ItemsAdded = foundItemIds.Count,
            ItemsMissing = missingItems.Count,
            MissingItems = missingItems
        };
    }

    /// <summary>
    /// Creates a new playlist with items using the LinkedChildren approach.
    /// This is the method that actually works, as discovered from the SmartLists plugin.
    /// </summary>
    /// <param name="playlistName">The name of the playlist.</param>
    /// <param name="itemIds">The list of item IDs to add to the playlist.</param>
    private async Task CreatePlaylistWithItemsAsync(string playlistName, List<Guid> itemIds)
    {
        _logger.LogDebug("Creating playlist '{PlaylistName}' with {ItemCount} items using LinkedChildren approach",
            playlistName, itemIds.Count);

        // Ensure we have a valid user ID
        var userId = _userId ?? Guid.Empty;
        if (userId == Guid.Empty)
        {
            _logger.LogWarning("No user ID provided, using empty GUID. Playlist may not be visible to users.");
        }

        // Step 1: Create the playlist (empty)
        var result = await _playlistManager.CreatePlaylist(new MediaBrowser.Model.Playlists.PlaylistCreationRequest
        {
            Name = playlistName,
            UserId = userId,
            Public = true, // Make it public so all users can see it
        });

        _logger.LogDebug("Playlist creation result: ID = {PlaylistId}", result.Id);

        // Step 2: Get the newly created playlist object
        if (_libraryManager.GetItemById(result.Id) is MediaBrowser.Controller.Playlists.Playlist newPlaylist)
        {
            _logger.LogDebug("Retrieved new playlist: Name = {Name}, ID = {PlaylistId}",
                newPlaylist.Name, newPlaylist.Id);

            // Step 3: Build LinkedChildren array with both ItemId and Path
            var linkedChildren = itemIds
                .Select(itemId =>
                {
                    var item = _libraryManager.GetItemById(itemId);
                    if (item == null)
                    {
                        _logger.LogWarning("Item with ID {ItemId} not found when building LinkedChildren", itemId);
                        return null;
                    }
                    return new LinkedChild
                    {
                        ItemId = itemId,
                        Path = item.Path ?? string.Empty
                    };
                })
                .Where(lc => lc != null)
                .ToArray()!;

            _logger.LogDebug("Built {Count} LinkedChildren for playlist '{PlaylistName}'",
                linkedChildren.Length, playlistName);

            // Step 4: Set LinkedChildren directly on the playlist object (THE KEY!)
            newPlaylist.LinkedChildren = linkedChildren;

            // Step 5: Persist changes using UpdateToRepositoryAsync (THE KEY!)
            await newPlaylist.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, CancellationToken.None);

            _logger.LogInformation("Successfully created playlist '{PlaylistName}' with {ItemCount} items",
                playlistName, linkedChildren.Length);
        }
        else
        {
            _logger.LogError("Failed to retrieve newly created playlist with ID {PlaylistId}", result.Id);
            throw new InvalidOperationException($"Failed to retrieve playlist after creation: {result.Id}");
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
}
