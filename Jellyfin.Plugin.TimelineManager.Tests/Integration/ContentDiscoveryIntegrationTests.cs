using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.TimelineManager.Models;
using Jellyfin.Plugin.TimelineManager.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.TimelineManager.Tests.Integration;

/// <summary>
/// Integration tests for the content discovery system components working together.
/// </summary>
public class ContentDiscoveryIntegrationTests
{
    /// <summary>
    /// Test that the complete content discovery pipeline works end-to-end.
    /// </summary>
    [Fact]
    public void ContentDiscoveryPipeline_WorksEndToEnd()
    {
        // Arrange - Create mock library items
        var libraryItems = new List<BaseItem>
        {
            CreateMockMovie("1771", "tt0371746", "Captain America: The First Avenger"),
            CreateMockMovie("299537", "tt4154664", "Captain Marvel"),
            CreateMockEpisode("85271", "tt2364582", "WandaVision Episode 1")
        };

        // Create timeline configuration
        var universe = new Universe
        {
            Key = "mcu",
            Name = "Marvel Cinematic Universe",
            Items = new List<TimelineItem>
            {
                new() { ProviderId = "1771", ProviderName = "tmdb", Type = "movie" },
                new() { ProviderId = "299537", ProviderName = "tmdb", Type = "movie" },
                new() { ProviderId = "85271", ProviderName = "tmdb", Type = "episode" }
            }
        };

        // Setup services
        var mockLibraryManager = new Mock<ILibraryManager>();
        mockLibraryManager.Setup(x => x.GetItemList(It.IsAny<InternalItemsQuery>()))
                        .Returns(libraryItems);

        var mockLookupLogger = new Mock<ILogger<ContentLookupService>>();
        var lookupService = new ContentLookupService(mockLookupLogger.Object, mockLibraryManager.Object);

        var mockMatchingLogger = new Mock<ILogger<ProviderMatchingService>>();
        var matchingService = new ProviderMatchingService(mockMatchingLogger.Object, lookupService);

        var mockMixedContentLogger = new Mock<ILogger<MixedContentService>>();
        var mixedContentService = new MixedContentService(mockMixedContentLogger.Object, matchingService);

        // Act - Execute the complete pipeline
        lookupService.BuildLookupTables();
        var result = mixedContentService.ProcessMixedContentUniverse(universe);

        // Assert - Verify the pipeline worked correctly
        Assert.True(result.ValidationResult.IsValid);
        Assert.True(result.ContentTypeAnalysis.IsMixedContent);
        Assert.Equal(2, result.ContentTypeAnalysis.MovieCount);
        Assert.Equal(1, result.ContentTypeAnalysis.EpisodeCount);
        Assert.Equal(3, result.MatchingResult.MatchedItems.Count);
        Assert.Empty(result.MatchingResult.MissingItems);
        Assert.Equal(2, result.MovieItems.Count);
        Assert.Equal(1, result.EpisodeItems.Count);
    }

    /// <summary>
    /// Test that the system handles missing items gracefully.
    /// </summary>
    [Fact]
    public void ContentDiscoveryPipeline_HandlesMissingItemsGracefully()
    {
        // Arrange - Create library with only some items
        var libraryItems = new List<BaseItem>
        {
            CreateMockMovie("1771", "tt0371746", "Captain America: The First Avenger")
            // Missing the other items
        };

        var universe = new Universe
        {
            Key = "mcu",
            Name = "Marvel Cinematic Universe",
            Items = new List<TimelineItem>
            {
                new() { ProviderId = "1771", ProviderName = "tmdb", Type = "movie" },
                new() { ProviderId = "299537", ProviderName = "tmdb", Type = "movie" }, // Missing
                new() { ProviderId = "85271", ProviderName = "tmdb", Type = "episode" } // Missing
            }
        };

        // Setup services
        var mockLibraryManager = new Mock<ILibraryManager>();
        mockLibraryManager.Setup(x => x.GetItemList(It.IsAny<InternalItemsQuery>()))
                        .Returns(libraryItems);

        var mockLookupLogger = new Mock<ILogger<ContentLookupService>>();
        var lookupService = new ContentLookupService(mockLookupLogger.Object, mockLibraryManager.Object);

        var mockMatchingLogger = new Mock<ILogger<ProviderMatchingService>>();
        var matchingService = new ProviderMatchingService(mockMatchingLogger.Object, lookupService);

        var mockMixedContentLogger = new Mock<ILogger<MixedContentService>>();
        var mixedContentService = new MixedContentService(mockMixedContentLogger.Object, matchingService);

        // Act
        lookupService.BuildLookupTables();
        var result = mixedContentService.ProcessMixedContentUniverse(universe);

        // Assert - Should handle missing items gracefully
        Assert.True(result.ValidationResult.IsValid);
        Assert.Equal(1, result.MatchingResult.MatchedItems.Count);
        Assert.Equal(2, result.MatchingResult.MissingItems.Count);
        Assert.Contains("tmdb_299537", result.MatchingResult.MissingItems);
        Assert.Contains("tmdb_85271", result.MatchingResult.MissingItems);
    }

    /// <summary>
    /// Test that lookup performance is consistent.
    /// </summary>
    [Fact]
    public void ContentLookupService_PerformanceIsConsistent()
    {
        // Arrange - Create a larger library
        var libraryItems = new List<BaseItem>();
        for (int i = 1; i <= 100; i++)
        {
            libraryItems.Add(CreateMockMovie(i.ToString(), $"tt{i + 1000000}", $"Movie {i}"));
        }

        var mockLibraryManager = new Mock<ILibraryManager>();
        mockLibraryManager.Setup(x => x.GetItemList(It.IsAny<InternalItemsQuery>()))
                        .Returns(libraryItems);

        var mockLogger = new Mock<ILogger<ContentLookupService>>();
        var lookupService = new ContentLookupService(mockLogger.Object, mockLibraryManager.Object);

        // Act
        lookupService.BuildLookupTables();

        // Test multiple lookups
        var lookupTimes = new List<TimeSpan>();
        for (int i = 1; i <= 10; i++)
        {
            var start = DateTime.UtcNow;
            var result = lookupService.FindItemByProviderId(i.ToString(), "tmdb", "movie");
            var elapsed = DateTime.UtcNow - start;
            lookupTimes.Add(elapsed);

            // Assert each lookup succeeds
            Assert.NotNull(result);
        }

        // Assert performance is consistent (all lookups should be very fast)
        var maxTime = lookupTimes.Max();
        var avgTime = TimeSpan.FromTicks((long)lookupTimes.Average(t => t.Ticks));
        
        Assert.True(maxTime.TotalMilliseconds < 10, "Lookup should be very fast");
        Assert.True(avgTime.TotalMilliseconds < 5, "Average lookup should be very fast");
    }

    /// <summary>
    /// Test that Provider_ID matching is accurate and consistent.
    /// </summary>
    [Fact]
    public void ProviderMatchingService_IsAccurateAndConsistent()
    {
        // Arrange
        var libraryItems = new List<BaseItem>
        {
            CreateMockMovie("1771", "tt0371746", "Captain America"),
            CreateMockEpisode("85271", "tt2364582", "WandaVision Ep1")
        };

        var timelineItems = new List<TimelineItem>
        {
            new() { ProviderId = "1771", ProviderName = "tmdb", Type = "movie" },
            new() { ProviderId = "tt0371746", ProviderName = "imdb", Type = "movie" },
            new() { ProviderId = "85271", ProviderName = "tmdb", Type = "episode" },
            new() { ProviderId = "tt2364582", ProviderName = "imdb", Type = "episode" }
        };

        var mockLibraryManager = new Mock<ILibraryManager>();
        mockLibraryManager.Setup(x => x.GetItemList(It.IsAny<InternalItemsQuery>()))
                        .Returns(libraryItems);

        var mockLookupLogger = new Mock<ILogger<ContentLookupService>>();
        var lookupService = new ContentLookupService(mockLookupLogger.Object, mockLibraryManager.Object);

        var mockMatchingLogger = new Mock<ILogger<ProviderMatchingService>>();
        var matchingService = new ProviderMatchingService(mockMatchingLogger.Object, lookupService);

        // Act
        lookupService.BuildLookupTables();

        // Test individual matching
        var individualResults = new Dictionary<TimelineItem, Guid?>();
        foreach (var item in timelineItems)
        {
            individualResults[item] = matchingService.MatchTimelineItem(item);
        }

        // Test batch matching
        var batchResults = matchingService.MatchTimelineItems(timelineItems);

        // Assert - Individual and batch results should be consistent
        foreach (var item in timelineItems)
        {
            var individualResult = individualResults[item];
            var batchResult = batchResults.TryGetValue(item, out var batchId) ? (Guid?)batchId : null;

            Assert.Equal(individualResult, batchResult);
            Assert.NotNull(individualResult); // All items should match
        }
    }

    /// <summary>
    /// Creates a mock Movie with provider IDs.
    /// </summary>
    private static Movie CreateMockMovie(string tmdbId, string imdbId, string name)
    {
        var movie = new Mock<Movie>();
        movie.Setup(x => x.Id).Returns(Guid.NewGuid());
        movie.Setup(x => x.Name).Returns(name);
        movie.Setup(x => x.ProviderIds).Returns(new Dictionary<string, string>
        {
            ["Tmdb"] = tmdbId,
            ["Imdb"] = imdbId
        });
        return movie.Object;
    }

    /// <summary>
    /// Creates a mock Episode with provider IDs.
    /// </summary>
    private static Episode CreateMockEpisode(string tmdbId, string imdbId, string name)
    {
        var episode = new Mock<Episode>();
        episode.Setup(x => x.Id).Returns(Guid.NewGuid());
        episode.Setup(x => x.Name).Returns(name);
        episode.Setup(x => x.ProviderIds).Returns(new Dictionary<string, string>
        {
            ["Tmdb"] = tmdbId,
            ["Imdb"] = imdbId
        });
        return episode.Object;
    }
}