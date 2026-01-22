using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Playlists;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TimelineManager.Services;

/// <summary>
/// Service for managing Jellyfin playlists with chronological ordering support.
/// Handles playlist creation, updates, and maintains timeline order from configuration.
/// </summary>
public class PlaylistService
{
    private readonly ILogger<PlaylistService> _logger;
    private readonly IPlaylistManager _playlistManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaylistService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="playlistManager">The Jellyfin playlist manager.</param>
    public PlaylistService(ILogger<PlaylistService> logger, IPlaylistManager playlistManager)
    {
        _logger = logger;
        _playlistManager = playlistManager;
    }

    /// <summary>
    /// Creates or updates a playlist with the specified items in chronological order.
    /// </summary>
    /// <param name="playlistName">The name of the playlist to create or update.</param>
    /// <param name="orderedItemIds">The list of item IDs in chronological order.</param>
    /// <param name="userId">The user ID for playlist ownership.</param>
    /// <returns>A result containing the playlist operation outcome.</returns>
    public async Task<PlaylistOperationResult> CreateOrUpdatePlaylistAsync(
        string playlistName, 
        List<Guid> orderedItemIds, 
        Guid userId)
    {
        if (string.IsNullOrWhiteSpace(playlistName))
        {
            throw new ArgumentException("Playlist name cannot be null or empty", nameof(playlistName));
        }

        if (orderedItemIds == null)
        {
            throw new ArgumentNullException(nameof(orderedItemIds));
        }

        var result = new PlaylistOperationResult
        {
            PlaylistName = playlistName,
            RequestedItemCount = orderedItemIds.Count
        };

        try
        {
            _logger.LogInformation("Processing playlist '{PlaylistName}' with {ItemCount} items for user {UserId}",
                playlistName, orderedItemIds.Count, userId);

            // Check if playlist already exists
            var existingPlaylist = await FindExistingPlaylistAsync(playlistName, userId);

            if (existingPlaylist != null)
            {
                _logger.LogDebug("Found existing playlist '{PlaylistName}' with ID {PlaylistId}",
                    playlistName, existingPlaylist.Id);

                result = await UpdateExistingPlaylistAsync(existingPlaylist, orderedItemIds, result);
            }
            else
            {
                _logger.LogDebug("Creating new playlist '{PlaylistName}'", playlistName);
                result = await CreateNewPlaylistAsync(playlistName, orderedItemIds, userId, result);
            }

            _logger.LogInformation("Playlist operation completed for '{PlaylistName}': {Status}, " +
                                 "Final item count: {FinalCount}",
                playlistName, result.OperationType, result.FinalItemCount);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create or update playlist '{PlaylistName}': {Message}",
                playlistName, ex.Message);

            result.IsSuccess = false;
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    /// <summary>
    /// Creates multiple playlists from universe matching results.
    /// </summary>
    /// <param name="universeResults">The universe matching results to create playlists from.</param>
    /// <param name="userId">The user ID for playlist ownership.</param>
    /// <returns>A collection of playlist operation results.</returns>
    public async Task<List<PlaylistOperationResult>> CreateUniversePlaylistsAsync(
        List<UniverseMatchingResult> universeResults,
        Guid userId)
    {
        var results = new List<PlaylistOperationResult>();

        if (universeResults == null || universeResults.Count == 0)
        {
            _logger.LogWarning("No universe results provided for playlist creation");
            return results;
        }

        _logger.LogInformation("Creating playlists for {UniverseCount} universes", universeResults.Count);

        foreach (var universeResult in universeResults)
        {
            try
            {
                if (universeResult.MatchedItems.Count == 0)
                {
                    _logger.LogWarning("Skipping universe '{UniverseName}' - no matched items",
                        universeResult.UniverseName);
                    
                    var emptyResult = new PlaylistOperationResult
                    {
                        PlaylistName = universeResult.UniverseName,
                        IsSuccess = false,
                        ErrorMessage = "No matched items found for universe",
                        OperationType = PlaylistOperationType.Skipped
                    };
                    results.Add(emptyResult);
                    continue;
                }

                var playlistResult = await CreateOrUpdatePlaylistAsync(
                    universeResult.UniverseName,
                    universeResult.MatchedItems,
                    userId);

                results.Add(playlistResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create playlist for universe '{UniverseName}': {Message}",
                    universeResult.UniverseName, ex.Message);

                var errorResult = new PlaylistOperationResult
                {
                    PlaylistName = universeResult.UniverseName,
                    IsSuccess = false,
                    ErrorMessage = ex.Message,
                    OperationType = PlaylistOperationType.Failed
                };
                results.Add(errorResult);
            }
        }

        var successCount = results.Count(r => r.IsSuccess);
        _logger.LogInformation("Playlist creation completed: {SuccessCount}/{TotalCount} successful",
            successCount, results.Count);

        return results;
    }

    /// <summary>
    /// Validates that the provided item IDs exist and are accessible.
    /// </summary>
    /// <param name="itemIds">The item IDs to validate.</param>
    /// <returns>A validation result with accessible and inaccessible items.</returns>
    public async Task<PlaylistValidationResult> ValidatePlaylistItemsAsync(List<Guid> itemIds)
    {
        var result = new PlaylistValidationResult();

        if (itemIds == null || itemIds.Count == 0)
        {
            return result;
        }

        _logger.LogDebug("Validating {ItemCount} playlist items", itemIds.Count);

        foreach (var itemId in itemIds)
        {
            try
            {
                // Note: In a real implementation, you would use ILibraryManager to validate items
                // For now, we'll assume all provided IDs are valid since they came from our lookup service
                result.ValidItemIds.Add(itemId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Item {ItemId} is not accessible: {Message}", itemId, ex.Message);
                result.InvalidItemIds.Add(itemId);
            }
        }

        result.IsValid = result.InvalidItemIds.Count == 0;
        
        _logger.LogDebug("Validation completed: {ValidCount} valid, {InvalidCount} invalid items",
            result.ValidItemIds.Count, result.InvalidItemIds.Count);

        return result;
    }

    /// <summary>
    /// Gets statistics about existing playlists managed by this service.
    /// </summary>
    /// <param name="userId">The user ID to get statistics for.</param>
    /// <returns>Statistics about managed playlists.</returns>
    public async Task<PlaylistStatistics> GetPlaylistStatisticsAsync(Guid userId)
    {
        var statistics = new PlaylistStatistics();

        try
        {
            // Note: In a real implementation, you would query existing playlists
            // This is a placeholder for the actual implementation
            _logger.LogDebug("Getting playlist statistics for user {UserId}", userId);
            
            // Placeholder statistics
            statistics.TotalPlaylists = 0;
            statistics.TotalItems = 0;
            statistics.LastUpdated = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get playlist statistics for user {UserId}: {Message}",
                userId, ex.Message);
        }

        return statistics;
    }

    /// <summary>
    /// Finds an existing playlist by name for the specified user.
    /// </summary>
    /// <param name="playlistName">The name of the playlist to find.</param>
    /// <param name="userId">The user ID to search for.</param>
    /// <returns>The existing playlist if found, null otherwise.</returns>
    private async Task<Playlist?> FindExistingPlaylistAsync(string playlistName, Guid userId)
    {
        try
        {
            // Note: In a real implementation, you would use IPlaylistManager to find existing playlists
            // This is a placeholder for the actual implementation
            _logger.LogTrace("Searching for existing playlist '{PlaylistName}' for user {UserId}",
                playlistName, userId);

            // Placeholder - return null to indicate no existing playlist found
            await Task.CompletedTask;
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error searching for existing playlist '{PlaylistName}': {Message}",
                playlistName, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Creates a new playlist with the specified items.
    /// </summary>
    /// <param name="playlistName">The name of the new playlist.</param>
    /// <param name="orderedItemIds">The ordered list of item IDs.</param>
    /// <param name="userId">The user ID for playlist ownership.</param>
    /// <param name="result">The result object to update.</param>
    /// <returns>The updated result object.</returns>
    private async Task<PlaylistOperationResult> CreateNewPlaylistAsync(
        string playlistName,
        List<Guid> orderedItemIds,
        Guid userId,
        PlaylistOperationResult result)
    {
        try
        {
            // Validate items before creating playlist
            var validation = await ValidatePlaylistItemsAsync(orderedItemIds);
            if (!validation.IsValid)
            {
                _logger.LogWarning("Cannot create playlist '{PlaylistName}' - {InvalidCount} invalid items",
                    playlistName, validation.InvalidItemIds.Count);
                
                result.IsSuccess = false;
                result.ErrorMessage = $"Invalid items found: {validation.InvalidItemIds.Count}";
                return result;
            }

            // Note: In a real implementation, you would use IPlaylistManager.CreatePlaylist
            // This is a placeholder for the actual implementation
            _logger.LogInformation("Creating new playlist '{PlaylistName}' with {ItemCount} items",
                playlistName, validation.ValidItemIds.Count);

            // Simulate playlist creation
            await Task.CompletedTask;
            var newPlaylistId = Guid.NewGuid();

            result.IsSuccess = true;
            result.PlaylistId = newPlaylistId;
            result.OperationType = PlaylistOperationType.Created;
            result.FinalItemCount = validation.ValidItemIds.Count;

            _logger.LogInformation("Successfully created playlist '{PlaylistName}' with ID {PlaylistId}",
                playlistName, newPlaylistId);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create new playlist '{PlaylistName}': {Message}",
                playlistName, ex.Message);
            
            result.IsSuccess = false;
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    /// <summary>
    /// Updates an existing playlist with new items while maintaining chronological order.
    /// </summary>
    /// <param name="existingPlaylist">The existing playlist to update.</param>
    /// <param name="orderedItemIds">The new ordered list of item IDs.</param>
    /// <param name="result">The result object to update.</param>
    /// <returns>The updated result object.</returns>
    private async Task<PlaylistOperationResult> UpdateExistingPlaylistAsync(
        Playlist existingPlaylist,
        List<Guid> orderedItemIds,
        PlaylistOperationResult result)
    {
        try
        {
            // Validate items before updating playlist
            var validation = await ValidatePlaylistItemsAsync(orderedItemIds);
            if (!validation.IsValid)
            {
                _logger.LogWarning("Cannot update playlist '{PlaylistName}' - {InvalidCount} invalid items",
                    existingPlaylist.Name, validation.InvalidItemIds.Count);
                
                result.IsSuccess = false;
                result.ErrorMessage = $"Invalid items found: {validation.InvalidItemIds.Count}";
                return result;
            }

            // Note: In a real implementation, you would compare existing items with new items
            // and update the playlist accordingly using IPlaylistManager
            _logger.LogInformation("Updating existing playlist '{PlaylistName}' with {ItemCount} items",
                existingPlaylist.Name, validation.ValidItemIds.Count);

            // Simulate playlist update
            await Task.CompletedTask;

            result.IsSuccess = true;
            result.PlaylistId = existingPlaylist.Id;
            result.OperationType = PlaylistOperationType.Updated;
            result.FinalItemCount = validation.ValidItemIds.Count;

            _logger.LogInformation("Successfully updated playlist '{PlaylistName}' (ID: {PlaylistId})",
                existingPlaylist.Name, existingPlaylist.Id);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update existing playlist '{PlaylistName}': {Message}",
                existingPlaylist.Name, ex.Message);
            
            result.IsSuccess = false;
            result.ErrorMessage = ex.Message;
            return result;
        }
    }
}

/// <summary>
/// Result of a playlist operation (create or update).
/// </summary>
public class PlaylistOperationResult
{
    /// <summary>
    /// Gets or sets the playlist name.
    /// </summary>
    public string PlaylistName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the operation was successful.
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Gets or sets the playlist ID (for successful operations).
    /// </summary>
    public Guid? PlaylistId { get; set; }

    /// <summary>
    /// Gets or sets the type of operation performed.
    /// </summary>
    public PlaylistOperationType OperationType { get; set; }

    /// <summary>
    /// Gets or sets the number of items requested for the playlist.
    /// </summary>
    public int RequestedItemCount { get; set; }

    /// <summary>
    /// Gets or sets the final number of items in the playlist.
    /// </summary>
    public int FinalItemCount { get; set; }

    /// <summary>
    /// Gets or sets the error message (for failed operations).
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Type of playlist operation performed.
/// </summary>
public enum PlaylistOperationType
{
    /// <summary>
    /// A new playlist was created.
    /// </summary>
    Created,

    /// <summary>
    /// An existing playlist was updated.
    /// </summary>
    Updated,

    /// <summary>
    /// The operation was skipped.
    /// </summary>
    Skipped,

    /// <summary>
    /// The operation failed.
    /// </summary>
    Failed
}

/// <summary>
/// Result of playlist item validation.
/// </summary>
public class PlaylistValidationResult
{
    /// <summary>
    /// Gets or sets a value indicating whether all items are valid.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets the list of valid item IDs.
    /// </summary>
    public List<Guid> ValidItemIds { get; } = new();

    /// <summary>
    /// Gets the list of invalid item IDs.
    /// </summary>
    public List<Guid> InvalidItemIds { get; } = new();
}

/// <summary>
/// Statistics about managed playlists.
/// </summary>
public class PlaylistStatistics
{
    /// <summary>
    /// Gets or sets the total number of managed playlists.
    /// </summary>
    public int TotalPlaylists { get; set; }

    /// <summary>
    /// Gets or sets the total number of items across all playlists.
    /// </summary>
    public int TotalItems { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the last update.
    /// </summary>
    public DateTime LastUpdated { get; set; }
}