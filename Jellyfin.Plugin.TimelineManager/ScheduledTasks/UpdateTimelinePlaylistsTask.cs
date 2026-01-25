using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.TimelineManager.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Playlists;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TimelineManager.ScheduledTasks;

/// <summary>
/// Scheduled task that automatically updates timeline playlists with current library content.
/// </summary>
public class UpdateTimelinePlaylistsTask : IScheduledTask
{
    private readonly ILogger<UpdateTimelinePlaylistsTask> _logger;
    private readonly ILibraryManager _libraryManager;
    private readonly IPlaylistManager _playlistManager;
    private readonly IUserManager _userManager;
    private readonly UniverseManagementService _universeManagementService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateTimelinePlaylistsTask"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="playlistManager">The playlist manager.</param>
    /// <param name="userManager">The user manager.</param>
    /// <param name="universeManagementService">The universe management service.</param>
    public UpdateTimelinePlaylistsTask(
        ILogger<UpdateTimelinePlaylistsTask> logger,
        ILibraryManager libraryManager,
        IPlaylistManager playlistManager,
        IUserManager userManager,
        UniverseManagementService universeManagementService)
    {
        _logger = logger;
        _libraryManager = libraryManager;
        _playlistManager = playlistManager;
        _userManager = userManager;
        _universeManagementService = universeManagementService;
    }

    /// <inheritdoc />
    public string Name => "Update Timeline Playlists";

    /// <inheritdoc />
    public string Category => "Universal Timeline Manager";

    /// <inheritdoc />
    public string Key => "UpdateTimelinePlaylists";

    /// <inheritdoc />
    public string Description => "Updates existing timeline playlists with current library content";

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[UpdateTimelinePlaylists] Starting playlist update task at {Timestamp}", DateTime.UtcNow);
        
        try
        {
            // Discover all universe files
            var universes = await _universeManagementService.GetAllUniversesAsync();
            var totalUniverses = universes.Count;
            
            _logger.LogInformation("[UpdateTimelinePlaylists] Found {Count} universe(s) to process", totalUniverses);
            
            if (totalUniverses == 0)
            {
                _logger.LogInformation("[UpdateTimelinePlaylists] No universes found, task completed");
                progress.Report(100);
                return;
            }
            
            // Initialize counters
            var processedUniverses = 0;
            var successCount = 0;
            var failureCount = 0;
            
            // Process each universe
            foreach (var universeMetadata in universes)
            {
                // Check for cancellation
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("[UpdateTimelinePlaylists] Task cancelled by user");
                    return;
                }
                
                try
                {
                    _logger.LogInformation("[UpdateTimelinePlaylists] Processing universe '{Name}' ({Current}/{Total})", 
                        universeMetadata.Name, processedUniverses + 1, totalUniverses);
                    
                    // Get first user for playlist creation (required by Jellyfin)
                    var users = _userManager.Users.ToList();
                    if (users.Count == 0)
                    {
                        _logger.LogError("[UpdateTimelinePlaylists] No users found in library, cannot create playlists");
                        failureCount++;
                        continue;
                    }
                    
                    // Create playlist using PlaylistCreationService with selective universe processing
                    var playlistService = new PlaylistCreationService(
                        LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<PlaylistCreationService>(),
                        _playlistManager,
                        _libraryManager,
                        userId: users[0].Id,
                        selectedUniverseFilenames: new List<string> { universeMetadata.Filename });
                    
                    var response = await playlistService.CreatePlaylistsAsync();
                    
                    if (response.Success)
                    {
                        successCount++;
                        var playlistResult = response.Playlists.FirstOrDefault();
                        if (playlistResult != null)
                        {
                            _logger.LogInformation("[UpdateTimelinePlaylists] Successfully updated playlist '{Name}'. Items added: {Count}", 
                                playlistResult.Name, playlistResult.ItemsAdded);
                        }
                    }
                    else
                    {
                        failureCount++;
                        _logger.LogError("[UpdateTimelinePlaylists] Failed to update playlist for universe '{Name}': {Errors}", 
                            universeMetadata.Name, string.Join(", ", response.Errors));
                    }
                }
                catch (Exception ex)
                {
                    failureCount++;
                    _logger.LogError(ex, "[UpdateTimelinePlaylists] Error processing universe '{Name}': {Message}", 
                        universeMetadata.Name, ex.Message);
                }
                finally
                {
                    processedUniverses++;
                    progress.Report((double)processedUniverses / totalUniverses * 100);
                }
            }
            
            // Log completion summary
            _logger.LogInformation("[UpdateTimelinePlaylists] Task completed. Processed: {Total}, Succeeded: {Success}, Failed: {Failed}", 
                processedUniverses, successCount, failureCount);
            
            // If all failed, throw exception to mark task as failed
            if (failureCount > 0 && successCount == 0)
            {
                throw new Exception($"All {failureCount} playlist updates failed");
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[UpdateTimelinePlaylists] Task was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[UpdateTimelinePlaylists] Task failed with error: {Message}", ex.Message);
            throw;
        }
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return new[]
        {
            new TaskTriggerInfo
            {
                Type = "DailyTrigger",
                TimeOfDayTicks = TimeSpan.FromHours(3).Ticks // 3:00 AM
            }
        };
    }
}
