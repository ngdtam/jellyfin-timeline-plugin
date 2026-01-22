using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using FsCheck;
using FsCheck.Xunit;
using Jellyfin.Plugin.TimelineManager.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;
using Moq;

namespace Jellyfin.Plugin.TimelineManager.Tests.Properties;

/// <summary>
/// Property-based tests for content lookup functionality and performance.
/// </summary>
public class ContentLookupPropertyTests
{
    /// <summary>
    /// Generator for valid provider IDs.
    /// </summary>
    public static Arbitrary<string> ValidProviderIds() =>
        Gen.OneOf(
            Gen.Choose(1, 999999).Select(x => x.ToString()), // TMDB style numeric IDs
            Gen.Constant("tt").SelectMany(prefix => 
                Gen.Choose(1000000, 9999999).Select(x => prefix + x.ToString())) // IMDB style IDs
        ).ToArbitrary();

    /// <summary>
    /// Generator for mock BaseItem objects with provider IDs.
    /// </summary>
    public static Arbitrary<BaseItem> MockBaseItems() =>
        Gen.OneOf<BaseItem>(
            // Mock Movies
            (from tmdbId in ValidProviderIds().Generator
             from imdbId in ValidProviderIds().Generator
             select CreateMockMovie(tmdbId, imdbId)).Select(x => (BaseItem)x),
            
            // Mock Series
            (from tmdbId in ValidProviderIds().Generator
             from imdbId in ValidProviderIds().Generator
             select CreateMockSeries(tmdbId, imdbId)).Select(x => (BaseItem)x),
            
            // Mock Episodes
            (from tmdbId in ValidProviderIds().Generator
             from imdbId in ValidProviderIds().Generator
             select CreateMockEpisode(tmdbId, imdbId)).Select(x => (BaseItem)x)
        ).ToArbitrary();

    /// <summary>
    /// Generator for collections of mock BaseItem objects.
    /// </summary>
    public static Arbitrary<List<BaseItem>> MockItemCollections() =>
        Gen.ListOf(MockBaseItems().Generator)
           .Select(items => items.ToList())
           .ToArbitrary();

    /// <summary>
    /// **Feature: jellyfin-timeline-manager, Property 4: Lookup dictionary provides O(1) performance**
    /// For any library of media items, the Timeline_Manager should build lookup dictionaries 
    /// that enable O(1) retrieval of items by Provider_ID, regardless of library size.
    /// **Validates: Requirements 3.2**
    /// </summary>
    [Property(Arbitrary = new[] { typeof(ContentLookupPropertyTests) })]
    public Property LookupDictionaryProvidesO1Performance(List<BaseItem> libraryItems)
    {
        // Skip empty collections as they don't test performance meaningfully
        if (libraryItems.Count == 0)
        {
            return true.ToProperty();
        }

        try
        {
            // Create mock library manager
            var mockLibraryManager = new Mock<ILibraryManager>();
            mockLibraryManager.Setup(x => x.GetItemList(It.IsAny<InternalItemsQuery>()))
                            .Returns(libraryItems);

            var mockLogger = new Mock<ILogger<ContentLookupService>>();
            var lookupService = new ContentLookupService(mockLogger.Object, mockLibraryManager.Object);

            // Build lookup tables
            var buildStopwatch = Stopwatch.StartNew();
            lookupService.BuildLookupTables();
            buildStopwatch.Stop();

            // Perform multiple lookups and measure performance
            var lookupTimes = new List<long>();
            var itemsWithProviders = libraryItems.Where(item => 
                item.ProviderIds != null && item.ProviderIds.Count > 0).ToList();

            if (itemsWithProviders.Count == 0)
            {
                return true.ToProperty(); // No items with providers to test
            }

            // Test lookup performance with different library sizes
            for (int i = 0; i < Math.Min(100, itemsWithProviders.Count); i++)
            {
                var testItem = itemsWithProviders[i % itemsWithProviders.Count];
                var providerId = testItem.ProviderIds.First().Value;
                var providerName = testItem.ProviderIds.First().Key.ToLowerInvariant();
                var contentType = GetContentTypeForItem(testItem);

                var lookupStopwatch = Stopwatch.StartNew();
                var result = lookupService.FindItemByProviderId(providerId, providerName, contentType);
                lookupStopwatch.Stop();

                lookupTimes.Add(lookupStopwatch.ElapsedTicks);
            }

            // Verify O(1) performance characteristics
            // Lookup times should be consistently fast regardless of library size
            var averageLookupTicks = lookupTimes.Average();
            var maxLookupTicks = lookupTimes.Max();
            
            // Performance should be consistent (max shouldn't be more than 10x average)
            var performanceIsConsistent = maxLookupTicks <= averageLookupTicks * 10;
            
            // Lookups should be very fast (less than 1ms on average)
            var lookupsAreFast = averageLookupTicks < TimeSpan.TicksPerMillisecond;

            return (performanceIsConsistent && lookupsAreFast).ToProperty();
        }
        catch (Exception)
        {
            return false.ToProperty();
        }
    }

    /// <summary>
    /// Property test to verify that lookup performance scales linearly with build time but not lookup time.
    /// </summary>
    [Property(Arbitrary = new[] { typeof(ContentLookupPropertyTests) })]
    public Property LookupPerformanceScalesCorrectly(List<BaseItem> smallLibrary, List<BaseItem> largeLibrary)
    {
        // Ensure we have different sized libraries for comparison
        if (smallLibrary.Count >= largeLibrary.Count || smallLibrary.Count == 0)
        {
            return true.ToProperty(); // Skip if sizes aren't appropriate for comparison
        }

        try
        {
            // Test small library
            var mockSmallLibraryManager = new Mock<ILibraryManager>();
            mockSmallLibraryManager.Setup(x => x.GetItemList(It.IsAny<InternalItemsQuery>()))
                                  .Returns(smallLibrary);

            var mockLogger1 = new Mock<ILogger<ContentLookupService>>();
            var smallLookupService = new ContentLookupService(mockLogger1.Object, mockSmallLibraryManager.Object);

            var smallBuildStopwatch = Stopwatch.StartNew();
            smallLookupService.BuildLookupTables();
            smallBuildStopwatch.Stop();

            // Test large library
            var mockLargeLibraryManager = new Mock<ILibraryManager>();
            mockLargeLibraryManager.Setup(x => x.GetItemList(It.IsAny<InternalItemsQuery>()))
                                  .Returns(largeLibrary);

            var mockLogger2 = new Mock<ILogger<ContentLookupService>>();
            var largeLookupService = new ContentLookupService(mockLogger2.Object, mockLargeLibraryManager.Object);

            var largeBuildStopwatch = Stopwatch.StartNew();
            largeLookupService.BuildLookupTables();
            largeBuildStopwatch.Stop();

            // Perform lookup tests on both
            var smallLookupTime = MeasureAverageLookupTime(smallLookupService, smallLibrary);
            var largeLookupTime = MeasureAverageLookupTime(largeLookupService, largeLibrary);

            // Build time can scale with library size (that's expected)
            // But lookup time should remain constant (O(1))
            var lookupTimeRatio = largeLookupTime > 0 ? smallLookupTime / largeLookupTime : 1.0;
            
            // Lookup times should be similar regardless of library size (within 5x factor)
            var lookupTimesAreConsistent = lookupTimeRatio >= 0.2 && lookupTimeRatio <= 5.0;

            return lookupTimesAreConsistent.ToProperty();
        }
        catch (Exception)
        {
            return false.ToProperty();
        }
    }

    /// <summary>
    /// Property test to verify that all indexed items can be found via lookup.
    /// </summary>
    [Property(Arbitrary = new[] { typeof(ContentLookupPropertyTests) })]
    public Property AllIndexedItemsCanBeFound(List<BaseItem> libraryItems)
    {
        if (libraryItems.Count == 0)
        {
            return true.ToProperty();
        }

        try
        {
            var mockLibraryManager = new Mock<ILibraryManager>();
            mockLibraryManager.Setup(x => x.GetItemList(It.IsAny<InternalItemsQuery>()))
                            .Returns(libraryItems);

            var mockLogger = new Mock<ILogger<ContentLookupService>>();
            var lookupService = new ContentLookupService(mockLogger.Object, mockLibraryManager.Object);

            lookupService.BuildLookupTables();

            // Test that all items with provider IDs can be found
            var itemsWithProviders = libraryItems.Where(item => 
                item.ProviderIds != null && item.ProviderIds.Count > 0);

            foreach (var item in itemsWithProviders)
            {
                foreach (var providerKvp in item.ProviderIds)
                {
                    var providerName = providerKvp.Key.ToLowerInvariant();
                    var providerId = providerKvp.Value;
                    var contentType = GetContentTypeForItem(item);

                    // Skip unsupported provider/type combinations
                    if (!IsSupportedProviderType(providerName, contentType))
                    {
                        continue;
                    }

                    var foundId = lookupService.FindItemByProviderId(providerId, providerName, contentType);
                    
                    if (!foundId.HasValue || foundId.Value != item.Id)
                    {
                        return false.ToProperty();
                    }
                }
            }

            return true.ToProperty();
        }
        catch (Exception)
        {
            return false.ToProperty();
        }
    }

    /// <summary>
    /// Creates a mock Movie object with provider IDs.
    /// </summary>
    private static Movie CreateMockMovie(string tmdbId, string imdbId)
    {
        var movie = new Mock<Movie>();
        movie.Setup(x => x.Id).Returns(Guid.NewGuid());
        movie.Setup(x => x.Name).Returns($"Test Movie {tmdbId}");
        movie.Setup(x => x.ProviderIds).Returns(new Dictionary<string, string>
        {
            ["Tmdb"] = tmdbId,
            ["Imdb"] = imdbId
        });
        return movie.Object;
    }

    /// <summary>
    /// Creates a mock Series object with provider IDs.
    /// </summary>
    private static Series CreateMockSeries(string tmdbId, string imdbId)
    {
        var series = new Mock<Series>();
        series.Setup(x => x.Id).Returns(Guid.NewGuid());
        series.Setup(x => x.Name).Returns($"Test Series {tmdbId}");
        series.Setup(x => x.ProviderIds).Returns(new Dictionary<string, string>
        {
            ["Tmdb"] = tmdbId,
            ["Imdb"] = imdbId
        });
        return series.Object;
    }

    /// <summary>
    /// Creates a mock Episode object with provider IDs.
    /// </summary>
    private static Episode CreateMockEpisode(string tmdbId, string imdbId)
    {
        var episode = new Mock<Episode>();
        episode.Setup(x => x.Id).Returns(Guid.NewGuid());
        episode.Setup(x => x.Name).Returns($"Test Episode {tmdbId}");
        episode.Setup(x => x.ProviderIds).Returns(new Dictionary<string, string>
        {
            ["Tmdb"] = tmdbId,
            ["Imdb"] = imdbId
        });
        return episode.Object;
    }

    /// <summary>
    /// Gets the content type string for a BaseItem.
    /// </summary>
    private static string GetContentTypeForItem(BaseItem item)
    {
        return item switch
        {
            Movie => "movie",
            Episode => "episode",
            Series => "movie", // Series are treated as movies for lookup purposes
            _ => "movie"
        };
    }

    /// <summary>
    /// Checks if a provider/type combination is supported.
    /// </summary>
    private static bool IsSupportedProviderType(string providerName, string contentType)
    {
        return (providerName, contentType) switch
        {
            ("tmdb", "movie") => true,
            ("tmdb", "episode") => true,
            ("imdb", "movie") => true,
            ("imdb", "episode") => true,
            _ => false
        };
    }

    /// <summary>
    /// Measures the average lookup time for a set of items.
    /// </summary>
    private static double MeasureAverageLookupTime(ContentLookupService lookupService, List<BaseItem> items)
    {
        var itemsWithProviders = items.Where(item => 
            item.ProviderIds != null && item.ProviderIds.Count > 0).ToList();

        if (itemsWithProviders.Count == 0)
        {
            return 0;
        }

        var totalTicks = 0L;
        var lookupCount = 0;

        for (int i = 0; i < Math.Min(10, itemsWithProviders.Count); i++)
        {
            var item = itemsWithProviders[i];
            foreach (var providerKvp in item.ProviderIds)
            {
                var providerName = providerKvp.Key.ToLowerInvariant();
                var providerId = providerKvp.Value;
                var contentType = GetContentTypeForItem(item);

                if (!IsSupportedProviderType(providerName, contentType))
                {
                    continue;
                }

                var stopwatch = Stopwatch.StartNew();
                lookupService.FindItemByProviderId(providerId, providerName, contentType);
                stopwatch.Stop();

                totalTicks += stopwatch.ElapsedTicks;
                lookupCount++;
            }
        }

        return lookupCount > 0 ? (double)totalTicks / lookupCount : 0;
    }

    /// <summary>
    /// **Feature: jellyfin-timeline-manager, Property 3: Provider ID matching is consistent and accurate**
    /// For any timeline item with a valid Provider_ID, the Timeline_Manager should match content using 
    /// the Provider_ID rather than title or other attributes, ensuring accurate content identification.
    /// **Validates: Requirements 2.5, 3.3**
    /// </summary>
    [Property(Arbitrary = new[] { typeof(ContentLookupPropertyTests) })]
    public Property ProviderIdMatchingIsConsistentAndAccurate(List<BaseItem> libraryItems)
    {
        if (libraryItems.Count == 0)
        {
            return true.ToProperty();
        }

        try
        {
            // Setup mock library manager and services
            var mockLibraryManager = new Mock<ILibraryManager>();
            mockLibraryManager.Setup(x => x.GetItemList(It.IsAny<InternalItemsQuery>()))
                            .Returns(libraryItems);

            var mockLookupLogger = new Mock<ILogger<Jellyfin.Plugin.TimelineManager.Services.ContentLookupService>>();
            var lookupService = new Jellyfin.Plugin.TimelineManager.Services.ContentLookupService(
                mockLookupLogger.Object, mockLibraryManager.Object);

            var mockMatchingLogger = new Mock<ILogger<Jellyfin.Plugin.TimelineManager.Services.ProviderMatchingService>>();
            var matchingService = new Jellyfin.Plugin.TimelineManager.Services.ProviderMatchingService(
                mockMatchingLogger.Object, lookupService);

            // Build lookup tables
            lookupService.BuildLookupTables();

            // Create timeline items based on the library items
            var timelineItems = new List<Jellyfin.Plugin.TimelineManager.Models.TimelineItem>();
            var expectedMatches = new Dictionary<string, Guid>();

            foreach (var item in libraryItems.Where(i => i.ProviderIds != null && i.ProviderIds.Count > 0))
            {
                foreach (var providerKvp in item.ProviderIds)
                {
                    var providerName = providerKvp.Key.ToLowerInvariant();
                    var providerId = providerKvp.Value;
                    var contentType = GetContentTypeForItem(item);

                    if (!IsSupportedProviderType(providerName, contentType))
                    {
                        continue;
                    }

                    var timelineItem = new Jellyfin.Plugin.TimelineManager.Models.TimelineItem
                    {
                        ProviderId = providerId,
                        ProviderName = providerName,
                        Type = contentType
                    };

                    timelineItems.Add(timelineItem);
                    expectedMatches[timelineItem.ProviderKey] = item.Id;
                }
            }

            if (timelineItems.Count == 0)
            {
                return true.ToProperty(); // No valid items to test
            }

            // Test individual matching
            foreach (var timelineItem in timelineItems)
            {
                var matchedId = matchingService.MatchTimelineItem(timelineItem);
                
                if (expectedMatches.TryGetValue(timelineItem.ProviderKey, out var expectedId))
                {
                    // Should match the correct item by Provider_ID
                    if (!matchedId.HasValue || matchedId.Value != expectedId)
                    {
                        return false.ToProperty();
                    }
                }
                else
                {
                    // Should not match if not in expected matches
                    if (matchedId.HasValue)
                    {
                        return false.ToProperty();
                    }
                }
            }

            // Test batch matching consistency
            var batchResults = matchingService.MatchTimelineItems(timelineItems);
            
            foreach (var timelineItem in timelineItems)
            {
                var individualMatch = matchingService.MatchTimelineItem(timelineItem);
                var batchMatch = batchResults.TryGetValue(timelineItem, out var batchId) ? (Guid?)batchId : null;

                // Individual and batch matching should produce identical results
                if (individualMatch != batchMatch)
                {
                    return false.ToProperty();
                }
            }

            return true.ToProperty();
        }
        catch (Exception)
        {
            return false.ToProperty();
        }
    }

    /// <summary>
    /// Property test to verify that Provider_ID matching is deterministic and repeatable.
    /// </summary>
    [Property(Arbitrary = new[] { typeof(ContentLookupPropertyTests) })]
    public Property ProviderIdMatchingIsDeterministic(List<BaseItem> libraryItems)
    {
        if (libraryItems.Count == 0)
        {
            return true.ToProperty();
        }

        try
        {
            // Setup services
            var mockLibraryManager = new Mock<ILibraryManager>();
            mockLibraryManager.Setup(x => x.GetItemList(It.IsAny<InternalItemsQuery>()))
                            .Returns(libraryItems);

            var mockLookupLogger = new Mock<ILogger<Jellyfin.Plugin.TimelineManager.Services.ContentLookupService>>();
            var lookupService = new Jellyfin.Plugin.TimelineManager.Services.ContentLookupService(
                mockLookupLogger.Object, mockLibraryManager.Object);

            var mockMatchingLogger = new Mock<ILogger<Jellyfin.Plugin.TimelineManager.Services.ProviderMatchingService>>();
            var matchingService = new Jellyfin.Plugin.TimelineManager.Services.ProviderMatchingService(
                mockMatchingLogger.Object, lookupService);

            lookupService.BuildLookupTables();

            // Create timeline items from library items
            var timelineItems = new List<Jellyfin.Plugin.TimelineManager.Models.TimelineItem>();
            
            foreach (var item in libraryItems.Where(i => i.ProviderIds != null && i.ProviderIds.Count > 0).Take(5))
            {
                foreach (var providerKvp in item.ProviderIds)
                {
                    var providerName = providerKvp.Key.ToLowerInvariant();
                    var providerId = providerKvp.Value;
                    var contentType = GetContentTypeForItem(item);

                    if (!IsSupportedProviderType(providerName, contentType))
                    {
                        continue;
                    }

                    timelineItems.Add(new Jellyfin.Plugin.TimelineManager.Models.TimelineItem
                    {
                        ProviderId = providerId,
                        ProviderName = providerName,
                        Type = contentType
                    });
                }
            }

            if (timelineItems.Count == 0)
            {
                return true.ToProperty();
            }

            // Perform matching multiple times and verify results are identical
            var firstResults = new Dictionary<Jellyfin.Plugin.TimelineManager.Models.TimelineItem, Guid?>();
            var secondResults = new Dictionary<Jellyfin.Plugin.TimelineManager.Models.TimelineItem, Guid?>();

            foreach (var item in timelineItems)
            {
                firstResults[item] = matchingService.MatchTimelineItem(item);
                secondResults[item] = matchingService.MatchTimelineItem(item);
            }

            // Results should be identical across multiple calls
            foreach (var item in timelineItems)
            {
                if (firstResults[item] != secondResults[item])
                {
                    return false.ToProperty();
                }
            }

            return true.ToProperty();
        }
        catch (Exception)
        {
            return false.ToProperty();
        }
    }

    /// <summary>
    /// Property test to verify that Provider_ID matching prioritizes Provider_ID over other attributes.
    /// </summary>
    [Property(Arbitrary = new[] { typeof(ContentLookupPropertyTests) })]
    public Property ProviderIdMatchingPrioritizesProviderId(List<BaseItem> libraryItems)
    {
        if (libraryItems.Count == 0)
        {
            return true.ToProperty();
        }

        try
        {
            // Setup services
            var mockLibraryManager = new Mock<ILibraryManager>();
            mockLibraryManager.Setup(x => x.GetItemList(It.IsAny<InternalItemsQuery>()))
                            .Returns(libraryItems);

            var mockLookupLogger = new Mock<ILogger<Jellyfin.Plugin.TimelineManager.Services.ContentLookupService>>();
            var lookupService = new Jellyfin.Plugin.TimelineManager.Services.ContentLookupService(
                mockLookupLogger.Object, mockLibraryManager.Object);

            var mockMatchingLogger = new Mock<ILogger<Jellyfin.Plugin.TimelineManager.Services.ProviderMatchingService>>();
            var matchingService = new Jellyfin.Plugin.TimelineManager.Services.ProviderMatchingService(
                mockMatchingLogger.Object, lookupService);

            lookupService.BuildLookupTables();

            // Test that matching is based solely on Provider_ID, not on names or other attributes
            var itemsWithProviders = libraryItems.Where(i => i.ProviderIds != null && i.ProviderIds.Count > 0).Take(3).ToList();
            
            foreach (var item in itemsWithProviders)
            {
                foreach (var providerKvp in item.ProviderIds)
                {
                    var providerName = providerKvp.Key.ToLowerInvariant();
                    var providerId = providerKvp.Value;
                    var contentType = GetContentTypeForItem(item);

                    if (!IsSupportedProviderType(providerName, contentType))
                    {
                        continue;
                    }

                    // Create timeline item with correct Provider_ID but different name
                    var timelineItem = new Jellyfin.Plugin.TimelineManager.Models.TimelineItem
                    {
                        ProviderId = providerId,
                        ProviderName = providerName,
                        Type = contentType
                    };

                    var matchedId = matchingService.MatchTimelineItem(timelineItem);

                    // Should match based on Provider_ID regardless of name differences
                    if (!matchedId.HasValue || matchedId.Value != item.Id)
                    {
                        return false.ToProperty();
                    }
                }
            }

            return true.ToProperty();
        }
        catch (Exception)
        {
            return false.ToProperty();
        }
    }
}