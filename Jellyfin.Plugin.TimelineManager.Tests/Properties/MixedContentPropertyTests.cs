using System;
using System.Collections.Generic;
using System.Linq;
using FsCheck;
using FsCheck.Xunit;
using Jellyfin.Plugin.TimelineManager.Models;
using Jellyfin.Plugin.TimelineManager.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;
using Moq;

namespace Jellyfin.Plugin.TimelineManager.Tests.Properties;

/// <summary>
/// Property-based tests for mixed content functionality.
/// </summary>
public class MixedContentPropertyTests
{
    /// <summary>
    /// Generator for mixed content timeline items (movies and episodes).
    /// </summary>
    public static Arbitrary<List<TimelineItem>> MixedContentTimelineItems() =>
        Gen.ListOf(
            Gen.OneOf(
                // Movie items
                from providerId in Gen.Choose(1, 999999).Select(x => x.ToString())
                from providerName in Gen.Elements("tmdb", "imdb")
                select new TimelineItem
                {
                    ProviderId = providerId,
                    ProviderName = providerName,
                    Type = "movie"
                },
                // Episode items
                from providerId in Gen.Choose(1, 999999).Select(x => x.ToString())
                from providerName in Gen.Elements("tmdb", "imdb")
                select new TimelineItem
                {
                    ProviderId = providerId,
                    ProviderName = providerName,
                    Type = "episode"
                }
            )
        ).Select(items => items.ToList()).ToArbitrary();

    /// <summary>
    /// Generator for universes containing mixed content.
    /// </summary>
    public static Arbitrary<Universe> MixedContentUniverses() =>
        (from key in Gen.Elements("mcu", "dceu", "star_wars")
         from name in Gen.Elements("Marvel Cinematic Universe", "DC Extended Universe", "Star Wars")
         from items in MixedContentTimelineItems().Generator
         select new Universe
         {
             Key = key,
             Name = name,
             Items = items
         }).ToArbitrary();

    /// <summary>
    /// Generator for mixed BaseItem collections (movies and episodes).
    /// </summary>
    public static Arbitrary<List<BaseItem>> MixedBaseItemCollections() =>
        Gen.ListOf(
            Gen.OneOf<BaseItem>(
                // Movies
                Gen.Choose(1, 999999).Select(x => CreateMockMovie(x.ToString(), $"tt{x + 1000000}")).Select(m => (BaseItem)m),
                // Episodes
                Gen.Choose(1, 999999).Select(x => CreateMockEpisode(x.ToString(), $"tt{x + 1000000}")).Select(e => (BaseItem)e)
            )
        ).Select(items => items.ToList()).ToArbitrary();

    /// <summary>
    /// **Feature: jellyfin-timeline-manager, Property 5: Mixed content types are supported throughout**
    /// For any universe configuration containing both movies and TV episodes, the Timeline_Manager 
    /// should successfully process all content types and include them in the generated playlist.
    /// **Validates: Requirements 3.4, 4.4**
    /// </summary>
    [Property(Arbitrary = new[] { typeof(MixedContentPropertyTests) })]
    public Property MixedContentTypesAreSupportedThroughout(Universe mixedUniverse)
    {
        // Skip empty universes
        if (mixedUniverse.Items == null || mixedUniverse.Items.Count == 0)
        {
            return true.ToProperty();
        }

        // Ensure we actually have mixed content
        var contentTypes = mixedUniverse.Items.Select(i => i.Type?.ToLowerInvariant()).Distinct().ToList();
        if (contentTypes.Count < 2 || !contentTypes.Contains("movie") || !contentTypes.Contains("episode"))
        {
            return true.ToProperty(); // Skip if not truly mixed content
        }

        try
        {
            // Create corresponding library items
            var libraryItems = CreateLibraryItemsFromTimeline(mixedUniverse.Items);

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

            // Build lookup tables
            lookupService.BuildLookupTables();

            // Process mixed content universe
            var result = mixedContentService.ProcessMixedContentUniverse(mixedUniverse);

            // Verify mixed content is properly processed
            var validationPassed = result.ValidationResult.IsValid;
            var mixedContentDetected = result.ContentTypeAnalysis.IsMixedContent;
            var hasMovies = result.ContentTypeAnalysis.MovieCount > 0;
            var hasEpisodes = result.ContentTypeAnalysis.EpisodeCount > 0;

            // Verify both content types are present and processed
            var bothTypesProcessed = hasMovies && hasEpisodes;

            // Verify matching works for both content types
            var movieMatches = result.MovieItems.Count;
            var episodeMatches = result.EpisodeItems.Count;
            var bothTypesMatched = movieMatches > 0 && episodeMatches > 0;

            return (validationPassed && mixedContentDetected && bothTypesProcessed && bothTypesMatched).ToProperty();
        }
        catch (Exception)
        {
            return false.ToProperty();
        }
    }

    /// <summary>
    /// Property test to verify that mixed content validation correctly identifies content type compatibility.
    /// </summary>
    [Property(Arbitrary = new[] { typeof(MixedContentPropertyTests) })]
    public Property MixedContentValidationIdentifiesCompatibility(List<TimelineItem> timelineItems)
    {
        if (timelineItems == null || timelineItems.Count == 0)
        {
            return true.ToProperty();
        }

        try
        {
            var mockLogger = new Mock<ILogger<MixedContentService>>();
            var mockMatchingService = new Mock<ProviderMatchingService>(
                Mock.Of<ILogger<ProviderMatchingService>>(),
                Mock.Of<ContentLookupService>());

            var mixedContentService = new MixedContentService(mockLogger.Object, mockMatchingService.Object);

            var validationResult = mixedContentService.ValidateMixedContentCompatibility(timelineItems);

            // Verify validation logic
            var hasValidItems = timelineItems.Any(item => 
                item != null && 
                !string.IsNullOrWhiteSpace(item.ProviderId) &&
                !string.IsNullOrWhiteSpace(item.ProviderName) &&
                !string.IsNullOrWhiteSpace(item.Type));

            var supportedTypes = new[] { "movie", "episode" };
            var hasOnlySupportedTypes = timelineItems.Where(item => item != null && !string.IsNullOrWhiteSpace(item.Type))
                                                   .All(item => supportedTypes.Contains(item.Type.ToLowerInvariant()));

            var supportedProviders = new[] { "tmdb", "imdb" };
            var hasOnlySupportedProviders = timelineItems.Where(item => item != null && !string.IsNullOrWhiteSpace(item.ProviderName))
                                                        .All(item => supportedProviders.Contains(item.ProviderName.ToLowerInvariant()));

            // If all items are valid and supported, validation should pass
            if (hasValidItems && hasOnlySupportedTypes && hasOnlySupportedProviders)
            {
                return validationResult.IsValid.ToProperty();
            }

            // If there are invalid items, validation should fail or provide appropriate errors
            if (!hasValidItems || !hasOnlySupportedTypes || !hasOnlySupportedProviders)
            {
                return (!validationResult.IsValid || validationResult.Errors.Count > 0).ToProperty();
            }

            return true.ToProperty();
        }
        catch (Exception)
        {
            return false.ToProperty();
        }
    }

    /// <summary>
    /// Property test to verify that content type analysis correctly categorizes mixed content.
    /// </summary>
    [Property(Arbitrary = new[] { typeof(MixedContentPropertyTests) })]
    public Property ContentTypeAnalysisCorrectlyCategorizesMixedContent(List<TimelineItem> timelineItems)
    {
        if (timelineItems == null)
        {
            return true.ToProperty();
        }

        try
        {
            var mockLogger = new Mock<ILogger<MixedContentService>>();
            var mockMatchingService = new Mock<ProviderMatchingService>(
                Mock.Of<ILogger<ProviderMatchingService>>(),
                Mock.Of<ContentLookupService>());

            var mixedContentService = new MixedContentService(mockLogger.Object, mockMatchingService.Object);

            var analysis = mixedContentService.AnalyzeContentTypes(timelineItems);

            // Verify analysis accuracy
            var expectedMovieCount = timelineItems.Count(item => 
                item != null && "movie".Equals(item.Type, StringComparison.OrdinalIgnoreCase));
            var expectedEpisodeCount = timelineItems.Count(item => 
                item != null && "episode".Equals(item.Type, StringComparison.OrdinalIgnoreCase));

            var movieCountCorrect = analysis.MovieCount == expectedMovieCount;
            var episodeCountCorrect = analysis.EpisodeCount == expectedEpisodeCount;

            // Verify mixed content detection
            var uniqueTypes = timelineItems.Where(item => item != null && !string.IsNullOrWhiteSpace(item.Type))
                                         .Select(item => item.Type.ToLowerInvariant())
                                         .Distinct()
                                         .Count();

            var mixedContentDetectionCorrect = analysis.IsMixedContent == (uniqueTypes > 1);

            // Verify total count
            var totalCountCorrect = analysis.TotalItems == timelineItems.Count;

            return (movieCountCorrect && episodeCountCorrect && mixedContentDetectionCorrect && totalCountCorrect).ToProperty();
        }
        catch (Exception)
        {
            return false.ToProperty();
        }
    }

    /// <summary>
    /// Property test to verify that mixed content maintains chronological order.
    /// </summary>
    [Property(Arbitrary = new[] { typeof(MixedContentPropertyTests) })]
    public Property MixedContentMaintainsChronologicalOrder(Universe mixedUniverse)
    {
        if (mixedUniverse.Items == null || mixedUniverse.Items.Count == 0)
        {
            return true.ToProperty();
        }

        try
        {
            // Create library items that match the timeline items
            var libraryItems = CreateLibraryItemsFromTimeline(mixedUniverse.Items);

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

            lookupService.BuildLookupTables();

            // Process the universe
            var result = mixedContentService.ProcessMixedContentUniverse(mixedUniverse);

            // Verify that the order of matched items corresponds to the original timeline order
            var originalOrder = mixedUniverse.Items.Select(item => item.ProviderKey).ToList();
            var matchedOrder = result.MatchingResult.MatchedTimelineItems.Select(item => item.ProviderKey).ToList();

            // The matched items should appear in the same relative order as the original timeline
            var orderPreserved = true;
            var originalIndex = 0;
            var matchedIndex = 0;

            while (originalIndex < originalOrder.Count && matchedIndex < matchedOrder.Count)
            {
                if (originalOrder[originalIndex] == matchedOrder[matchedIndex])
                {
                    matchedIndex++;
                }
                originalIndex++;
            }

            // All matched items should have been found in order
            orderPreserved = matchedIndex == matchedOrder.Count;

            return orderPreserved.ToProperty();
        }
        catch (Exception)
        {
            return false.ToProperty();
        }
    }

    /// <summary>
    /// Creates mock library items that correspond to timeline items for testing.
    /// </summary>
    private static List<BaseItem> CreateLibraryItemsFromTimeline(List<TimelineItem> timelineItems)
    {
        var libraryItems = new List<BaseItem>();

        foreach (var timelineItem in timelineItems)
        {
            if (timelineItem == null) continue;

            BaseItem? libraryItem = timelineItem.Type?.ToLowerInvariant() switch
            {
                "movie" => CreateMockMovie(timelineItem.ProviderId, $"tt{timelineItem.ProviderId}"),
                "episode" => CreateMockEpisode(timelineItem.ProviderId, $"tt{timelineItem.ProviderId}"),
                _ => null
            };

            if (libraryItem != null)
            {
                libraryItems.Add(libraryItem);
            }
        }

        return libraryItems;
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
}