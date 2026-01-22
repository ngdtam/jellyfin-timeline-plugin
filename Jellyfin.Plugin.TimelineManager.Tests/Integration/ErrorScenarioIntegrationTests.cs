using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.TimelineManager.Models;
using Jellyfin.Plugin.TimelineManager.Services;
using Jellyfin.Plugin.TimelineManager.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Playlists;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.TimelineManager.Tests.Integration;

/// <summary>
/// Integration tests for error scenarios and system resilience.
/// Tests various error conditions and verifies graceful handling and recovery.
/// </summary>
public class ErrorScenarioIntegrationTests
{
    /// <summary>
    /// Test that configuration service handles missing configuration files gracefully.
    /// </summary>
    [Fact]
    public async Task ConfigurationService_HandlesMissingFileGracefully()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<ConfigurationService>>();
        var nonExistentPath = Path.Combine(Path.GetTempPath(), "non_existent_config.json");
        var configService = new ConfigurationService(mockLogger.Object, nonExistentPath);

        // Act
        var result = await configService.LoadConfigurationAsync();

        // Assert
        Assert.Null(result);
        
        // Verify appropriate logging occurred
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("not found")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    /// <summary>
    /// Test that configuration service handles invalid JSON gracefully.
    /// </summary>
    [Fact]
    public async Task ConfigurationService_HandlesInvalidJsonGracefully()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<ConfigurationService>>();
        var tempFile = Path.GetTempFileName();
        
        try
        {
            // Write invalid JSON
            await File.WriteAllTextAsync(tempFile, "{ invalid json content }");
            
            var configService = new ConfigurationService(mockLogger.Object, tempFile);

            // Act
            var result = await configService.LoadConfigurationAsync();

            // Assert
            Assert.Null(result);
            
            // Verify JSON parsing error was logged
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("JSON parsing")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    /// <summary>
    /// Test that configuration service handles empty configuration gracefully.
    /// </summary>
    [Fact]
    public async Task ConfigurationService_HandlesEmptyConfigurationGracefully()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<ConfigurationService>>();
        var tempFile = Path.GetTempFileName();
        
        try
        {
            // Write empty JSON
            await File.WriteAllTextAsync(tempFile, "{}");
            
            var configService = new ConfigurationService(mockLogger.Object, tempFile);

            // Act
            var result = await configService.LoadConfigurationAsync();

            // Assert
            Assert.Null(result); // Should fail validation due to missing universes
            
            // Verify validation error was logged
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("validation failed")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    /// <summary>
    /// Test that timeline task handles library manager unavailability gracefully.
    /// </summary>
    [Fact]
    public async Task TimelineTask_HandlesLibraryManagerUnavailabilityGracefully()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<TimelineConfigTask>>();
        var mockLibraryManager = new Mock<ILibraryManager>();
        var mockPlaylistManager = new Mock<IPlaylistManager>();

        // Setup library manager to throw exception
        mockLibraryManager.Setup(x => x.GetItemsResult(It.IsAny<MediaBrowser.Controller.Entities.InternalItemsQuery>()))
                         .Throws(new InvalidOperationException("Library service unavailable"));

        var task = new TimelineConfigTask(mockLogger.Object, mockLibraryManager.Object, mockPlaylistManager.Object);

        // Act & Assert
        var cancellationToken = new CancellationToken();
        var progress = new Mock<IProgress<double>>();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => task.ExecuteAsync(progress.Object, cancellationToken));

        // Verify appropriate error logging occurred
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("service unavailable")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    /// <summary>
    /// Test that timeline task handles playlist manager unavailability gracefully.
    /// </summary>
    [Fact]
    public async Task TimelineTask_HandlesPlaylistManagerUnavailabilityGracefully()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<TimelineConfigTask>>();
        var mockLibraryManager = new Mock<ILibraryManager>();
        var mockPlaylistManager = new Mock<IPlaylistManager>();

        // Setup library manager to work but playlist manager to fail
        mockLibraryManager.Setup(x => x.GetItemsResult(It.IsAny<MediaBrowser.Controller.Entities.InternalItemsQuery>()))
                         .Returns(new MediaBrowser.Model.Querying.QueryResult<BaseItem>
                         {
                             Items = new BaseItem[0],
                             TotalRecordCount = 0
                         });

        mockLibraryManager.Setup(x => x.GetItemList(It.IsAny<MediaBrowser.Controller.Entities.InternalItemsQuery>()))
                         .Returns(new List<BaseItem>());

        // Setup playlist manager to throw exception
        mockPlaylistManager.Setup(x => x.GetPlaylists(It.IsAny<Guid>()))
                          .Throws(new InvalidOperationException("Playlist service unavailable"));

        var task = new TimelineConfigTask(mockLogger.Object, mockLibraryManager.Object, mockPlaylistManager.Object);

        // Act & Assert
        var cancellationToken = new CancellationToken();
        var progress = new Mock<IProgress<double>>();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => task.ExecuteAsync(progress.Object, cancellationToken));

        // Verify appropriate error logging occurred
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("service unavailable") || 
                                              v.ToString()!.Contains("Playlist service unavailable")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    /// <summary>
    /// Test that timeline task handles cancellation gracefully.
    /// </summary>
    [Fact]
    public async Task TimelineTask_HandlesCancellationGracefully()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<TimelineConfigTask>>();
        var mockLibraryManager = new Mock<ILibraryManager>();
        var mockPlaylistManager = new Mock<IPlaylistManager>();

        // Setup services to work normally
        mockLibraryManager.Setup(x => x.GetItemsResult(It.IsAny<MediaBrowser.Controller.Entities.InternalItemsQuery>()))
                         .Returns(new MediaBrowser.Model.Querying.QueryResult<BaseItem>
                         {
                             Items = new BaseItem[0],
                             TotalRecordCount = 0
                         });

        mockLibraryManager.Setup(x => x.GetItemList(It.IsAny<MediaBrowser.Controller.Entities.InternalItemsQuery>()))
                         .Returns(new List<BaseItem>());

        mockPlaylistManager.Setup(x => x.GetPlaylists(It.IsAny<Guid>()))
                          .Returns(new List<MediaBrowser.Controller.Playlists.Playlist>());

        var task = new TimelineConfigTask(mockLogger.Object, mockLibraryManager.Object, mockPlaylistManager.Object);

        // Act & Assert
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately
        
        var progress = new Mock<IProgress<double>>();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => task.ExecuteAsync(progress.Object, cts.Token));

        // Verify cancellation was logged appropriately
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("cancelled")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    /// <summary>
    /// Test that timeline task handles timeout scenarios gracefully.
    /// </summary>
    [Fact]
    public async Task TimelineTask_HandlesTimeoutGracefully()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<TimelineConfigTask>>();
        var mockLibraryManager = new Mock<ILibraryManager>();
        var mockPlaylistManager = new Mock<IPlaylistManager>();

        // Setup library manager to simulate slow operation
        mockLibraryManager.Setup(x => x.GetItemsResult(It.IsAny<MediaBrowser.Controller.Entities.InternalItemsQuery>()))
                         .Returns(() =>
                         {
                             // Simulate a slow operation that would trigger timeout
                             Thread.Sleep(100); // Short delay for test
                             return new MediaBrowser.Model.Querying.QueryResult<BaseItem>
                             {
                                 Items = new BaseItem[0],
                                 TotalRecordCount = 0
                             };
                         });

        var task = new TimelineConfigTask(mockLogger.Object, mockLibraryManager.Object, mockPlaylistManager.Object);

        // Act & Assert
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50)); // Very short timeout
        var progress = new Mock<IProgress<double>>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => task.ExecuteAsync(progress.Object, cts.Token));

        // Verify timeout handling was logged
        mockLogger.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("cancelled") || 
                                              v.ToString()!.Contains("timeout")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    /// <summary>
    /// Test that content lookup service handles empty library gracefully.
    /// </summary>
    [Fact]
    public void ContentLookupService_HandlesEmptyLibraryGracefully()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<ContentLookupService>>();
        var mockLibraryManager = new Mock<ILibraryManager>();

        // Setup empty library
        mockLibraryManager.Setup(x => x.GetItemList(It.IsAny<MediaBrowser.Controller.Entities.InternalItemsQuery>()))
                         .Returns(new List<BaseItem>());

        var lookupService = new ContentLookupService(mockLogger.Object, mockLibraryManager.Object);

        // Act
        lookupService.BuildLookupTables();
        var stats = lookupService.GetLookupStatistics();

        // Assert
        Assert.Equal(0, stats["TotalItemsIndexed"]);
        
        // Test lookup on empty library
        var result = lookupService.FindItemByProviderId("123", "tmdb", "movie");
        Assert.Null(result);

        // Verify warning about empty library was logged
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No items") || 
                                              v.ToString()!.Contains("empty")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    /// <summary>
    /// Test that mixed content service handles validation errors gracefully.
    /// </summary>
    [Fact]
    public void MixedContentService_HandlesValidationErrorsGracefully()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<MixedContentService>>();
        var mockProviderMatchingService = new Mock<ProviderMatchingService>(
            Mock.Of<ILogger<ProviderMatchingService>>(),
            Mock.Of<ContentLookupService>());

        var mixedContentService = new MixedContentService(mockLogger.Object, mockProviderMatchingService.Object);

        // Create invalid universe (null items)
        var invalidUniverse = new Universe
        {
            Key = "invalid",
            Name = "Invalid Universe",
            Items = null! // This should cause validation error
        };

        // Act
        var result = mixedContentService.ProcessMixedContentUniverse(invalidUniverse);

        // Assert
        Assert.False(result.ValidationResult.IsValid);
        Assert.NotEmpty(result.ValidationResult.Errors);
        
        // Should handle the error gracefully without throwing
        Assert.NotNull(result);
        Assert.Equal("invalid", result.UniverseKey);
    }

    /// <summary>
    /// Test that system handles memory pressure gracefully.
    /// </summary>
    [Fact]
    public void ContentLookupService_HandlesMemoryPressureGracefully()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<ContentLookupService>>();
        var mockLibraryManager = new Mock<ILibraryManager>();

        // Create a very large library to simulate memory pressure
        var largeLibrary = new List<BaseItem>();
        for (int i = 0; i < 10000; i++)
        {
            var mockMovie = new Mock<MediaBrowser.Controller.Entities.Movies.Movie>();
            mockMovie.Setup(x => x.Id).Returns(Guid.NewGuid());
            mockMovie.Setup(x => x.Name).Returns($"Movie {i}");
            mockMovie.Setup(x => x.ProviderIds).Returns(new Dictionary<string, string>
            {
                ["Tmdb"] = i.ToString()
            });
            largeLibrary.Add(mockMovie.Object);
        }

        mockLibraryManager.Setup(x => x.GetItemList(It.IsAny<MediaBrowser.Controller.Entities.InternalItemsQuery>()))
                         .Returns(largeLibrary);

        var lookupService = new ContentLookupService(mockLogger.Object, mockLibraryManager.Object);

        // Act & Assert - Should not throw OutOfMemoryException
        Exception? caughtException = null;
        try
        {
            lookupService.BuildLookupTables();
            var stats = lookupService.GetLookupStatistics();
            
            // Verify it processed the large library
            Assert.Equal(10000, stats["TotalItemsIndexed"]);
        }
        catch (Exception ex)
        {
            caughtException = ex;
        }

        // Should handle large datasets without memory issues
        Assert.Null(caughtException);
    }

    /// <summary>
    /// Test that error recovery guidance is provided for various scenarios.
    /// </summary>
    [Fact]
    public async Task ConfigurationService_ProvidesErrorRecoveryGuidance()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<ConfigurationService>>();
        
        // Test with various error scenarios
        var scenarios = new[]
        {
            new { Path = "/non/existent/path/config.json", ExpectedGuidance = "Create the configuration file" },
            new { Path = Path.GetTempFileName(), ExpectedGuidance = "JSON parsing" } // Will be invalid JSON
        };

        foreach (var scenario in scenarios)
        {
            try
            {
                if (File.Exists(scenario.Path))
                {
                    await File.WriteAllTextAsync(scenario.Path, "{ invalid json }");
                }

                var configService = new ConfigurationService(mockLogger.Object, scenario.Path);

                // Act
                var result = await configService.LoadConfigurationAsync();

                // Assert
                Assert.Null(result);

                // Verify recovery guidance was logged
                mockLogger.Verify(
                    x => x.Log(
                        LogLevel.Information,
                        It.IsAny<EventId>(),
                        It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Recovery Guidance") ||
                                                      v.ToString()!.Contains("Troubleshooting")),
                        It.IsAny<Exception>(),
                        It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                    Times.AtLeastOnce);
            }
            finally
            {
                if (File.Exists(scenario.Path))
                {
                    File.Delete(scenario.Path);
                }
            }
        }
    }
}