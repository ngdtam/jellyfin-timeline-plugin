using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using FsCheck;
using FsCheck.Xunit;
using Jellyfin.Plugin.TimelineManager.Models;

namespace Jellyfin.Plugin.TimelineManager.Tests.Properties;

/// <summary>
/// Property-based tests for configuration parsing functionality.
/// </summary>
public class ConfigurationPropertyTests
{
    /// <summary>
    /// Generator for valid provider names.
    /// </summary>
    public static Arbitrary<string> ValidProviderNames() =>
        Gen.Elements("tmdb", "imdb").ToArbitrary();

    /// <summary>
    /// Generator for valid content types.
    /// </summary>
    public static Arbitrary<string> ValidContentTypes() =>
        Gen.Elements("movie", "episode").ToArbitrary();

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
    /// Generator for valid universe keys.
    /// </summary>
    public static Arbitrary<string> ValidUniverseKeys() =>
        Gen.Elements("mcu", "dceu", "star_wars_canon", "star_wars_legends", "x_men", "fantastic_four")
           .ToArbitrary();

    /// <summary>
    /// Generator for valid universe names.
    /// </summary>
    public static Arbitrary<string> ValidUniverseNames() =>
        Gen.Elements(
            "Marvel Cinematic Universe",
            "DC Extended Universe", 
            "Star Wars Canon",
            "Star Wars Legends",
            "X-Men Universe",
            "Fantastic Four Universe"
        ).ToArbitrary();

    /// <summary>
    /// Generator for valid TimelineItem objects.
    /// </summary>
    public static Arbitrary<TimelineItem> ValidTimelineItems() =>
        (from providerId in ValidProviderIds().Generator
         from providerName in ValidProviderNames().Generator
         from type in ValidContentTypes().Generator
         select new TimelineItem
         {
             ProviderId = providerId,
             ProviderName = providerName,
             Type = type
         }).ToArbitrary();

    /// <summary>
    /// Generator for valid Universe objects.
    /// </summary>
    public static Arbitrary<Universe> ValidUniverses() =>
        (from key in ValidUniverseKeys().Generator
         from name in ValidUniverseNames().Generator
         from items in Gen.ListOf(ValidTimelineItems().Generator)
         select new Universe
         {
             Key = key,
             Name = name,
             Items = items.ToList()
         }).ToArbitrary();

    /// <summary>
    /// Generator for valid TimelineConfiguration objects.
    /// </summary>
    public static Arbitrary<TimelineConfiguration> ValidConfigurations() =>
        (from universes in Gen.NonEmptyListOf(ValidUniverses().Generator)
         select new TimelineConfiguration
         {
             Universes = universes.ToList()
         }).ToArbitrary();

    /// <summary>
    /// **Feature: jellyfin-timeline-manager, Property 1: Configuration parsing handles all valid universe structures**
    /// For any valid JSON configuration containing multiple universes with Key, Name, and Items arrays, 
    /// the Timeline_Manager should successfully parse all universes and extract their timeline items without errors.
    /// **Validates: Requirements 2.2, 2.4**
    /// </summary>
    [Property(Arbitrary = new[] { typeof(ConfigurationPropertyTests) })]
    public Property ConfigurationParsingHandlesAllValidUniverseStructures(TimelineConfiguration config)
    {
        try
        {
            // Serialize the configuration to JSON
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });

            // Deserialize back to verify round-trip parsing
            var parsedConfig = JsonSerializer.Deserialize<TimelineConfiguration>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            // Verify all universes are parsed correctly
            var allUniversesParsed = parsedConfig != null &&
                                   parsedConfig.Universes.Count == config.Universes.Count &&
                                   parsedConfig.Universes.All(u => !string.IsNullOrEmpty(u.Key)) &&
                                   parsedConfig.Universes.All(u => !string.IsNullOrEmpty(u.Name)) &&
                                   parsedConfig.Universes.All(u => u.Items != null);

            // Verify all timeline items are parsed correctly
            var allItemsParsed = parsedConfig?.Universes.All(universe =>
                universe.Items.All(item =>
                    !string.IsNullOrEmpty(item.ProviderId) &&
                    !string.IsNullOrEmpty(item.ProviderName) &&
                    !string.IsNullOrEmpty(item.Type)
                )
            ) ?? false;

            // Verify provider keys are generated correctly
            var providerKeysValid = parsedConfig?.Universes.All(universe =>
                universe.Items.All(item =>
                    item.ProviderKey == $"{item.ProviderName.ToLowerInvariant()}_{item.ProviderId}"
                )
            ) ?? false;

            return (allUniversesParsed && allItemsParsed && providerKeysValid).ToProperty();
        }
        catch (Exception)
        {
            // Any exception during parsing means the property failed
            return false.ToProperty();
        }
    }

    /// <summary>
    /// Property test to verify that all valid configurations maintain data integrity during serialization.
    /// </summary>
    [Property(Arbitrary = new[] { typeof(ConfigurationPropertyTests) })]
    public Property ValidConfigurationsMaintainDataIntegrity(TimelineConfiguration config)
    {
        try
        {
            // Serialize and deserialize
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var parsedConfig = JsonSerializer.Deserialize<TimelineConfiguration>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            // Verify data integrity
            if (parsedConfig == null) return false.ToProperty();

            // Check universe count matches
            if (parsedConfig.Universes.Count != config.Universes.Count) return false.ToProperty();

            // Check each universe's data integrity
            for (int i = 0; i < config.Universes.Count; i++)
            {
                var original = config.Universes[i];
                var parsed = parsedConfig.Universes[i];

                if (parsed.Key != original.Key || 
                    parsed.Name != original.Name ||
                    parsed.Items.Count != original.Items.Count)
                {
                    return false.ToProperty();
                }

                // Check each item's data integrity
                for (int j = 0; j < original.Items.Count; j++)
                {
                    var originalItem = original.Items[j];
                    var parsedItem = parsed.Items[j];

                    if (parsedItem.ProviderId != originalItem.ProviderId ||
                        parsedItem.ProviderName != originalItem.ProviderName ||
                        parsedItem.Type != originalItem.Type)
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
    /// Generator for invalid JSON strings that should cause parsing errors.
    /// </summary>
    public static Arbitrary<string> InvalidJsonStrings() =>
        Gen.OneOf(
            Gen.Constant("{"), // Incomplete JSON
            Gen.Constant("{ \"universes\": ["), // Incomplete array
            Gen.Constant("{ \"universes\": [ { \"key\": } ] }"), // Missing value
            Gen.Constant("{ \"universes\": [ { \"key\": \"test\", } ] }"), // Trailing comma
            Gen.Constant("{ \"invalid\": true }"), // Missing required fields
            Gen.Constant("null"), // Null JSON
            Gen.Constant(""), // Empty string
            Gen.Constant("not json at all"), // Invalid JSON
            Gen.Constant("{ \"universes\": null }") // Null universes
        ).ToArbitrary();

    /// <summary>
    /// Generator for configurations with invalid data that should fail validation.
    /// </summary>
    public static Arbitrary<TimelineConfiguration> InvalidConfigurations() =>
        Gen.OneOf(
            // Empty universes list
            Gen.Constant(new TimelineConfiguration { Universes = new List<Universe>() }),
            
            // Universe with empty key
            Gen.Constant(new TimelineConfiguration 
            { 
                Universes = new List<Universe> 
                { 
                    new Universe { Key = "", Name = "Test Universe", Items = new List<TimelineItem>() }
                }
            }),
            
            // Universe with key containing spaces
            Gen.Constant(new TimelineConfiguration 
            { 
                Universes = new List<Universe> 
                { 
                    new Universe { Key = "invalid key", Name = "Test Universe", Items = new List<TimelineItem>() }
                }
            }),
            
            // Universe with empty name
            Gen.Constant(new TimelineConfiguration 
            { 
                Universes = new List<Universe> 
                { 
                    new Universe { Key = "test", Name = "", Items = new List<TimelineItem>() }
                }
            }),
            
            // Timeline item with invalid provider
            Gen.Constant(new TimelineConfiguration 
            { 
                Universes = new List<Universe> 
                { 
                    new Universe 
                    { 
                        Key = "test", 
                        Name = "Test Universe", 
                        Items = new List<TimelineItem> 
                        { 
                            new TimelineItem { ProviderId = "123", ProviderName = "invalid", Type = "movie" }
                        }
                    }
                }
            }),
            
            // Timeline item with invalid type
            Gen.Constant(new TimelineConfiguration 
            { 
                Universes = new List<Universe> 
                { 
                    new Universe 
                    { 
                        Key = "test", 
                        Name = "Test Universe", 
                        Items = new List<TimelineItem> 
                        { 
                            new TimelineItem { ProviderId = "123", ProviderName = "tmdb", Type = "invalid" }
                        }
                    }
                }
            }),
            
            // Duplicate universe keys
            Gen.Constant(new TimelineConfiguration 
            { 
                Universes = new List<Universe> 
                { 
                    new Universe { Key = "duplicate", Name = "First Universe", Items = new List<TimelineItem>() },
                    new Universe { Key = "duplicate", Name = "Second Universe", Items = new List<TimelineItem>() }
                }
            })
        ).ToArbitrary();

    /// <summary>
    /// **Feature: jellyfin-timeline-manager, Property 2: Invalid configuration generates appropriate errors**
    /// For any malformed or invalid JSON configuration file, the Timeline_Manager should log descriptive errors 
    /// and gracefully skip processing without crashing.
    /// **Validates: Requirements 2.3, 7.1**
    /// </summary>
    [Property(Arbitrary = new[] { typeof(ConfigurationPropertyTests) })]
    public Property InvalidConfigurationGeneratesAppropriateErrors(string invalidJson)
    {
        try
        {
            // Attempt to deserialize invalid JSON
            var parsedConfig = JsonSerializer.Deserialize<TimelineConfiguration>(invalidJson, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            // If parsing succeeded but resulted in invalid configuration, validation should catch it
            if (parsedConfig != null)
            {
                var configService = new Jellyfin.Plugin.TimelineManager.Services.ConfigurationService(
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<Jellyfin.Plugin.TimelineManager.Services.ConfigurationService>.Instance);
                
                var validationResult = configService.ValidateConfiguration(parsedConfig);
                
                // Invalid configurations should fail validation
                return (!validationResult.IsValid && validationResult.Errors.Count > 0).ToProperty();
            }

            // If parsing failed, that's expected for invalid JSON
            return true.ToProperty();
        }
        catch (JsonException)
        {
            // JSON parsing exceptions are expected for malformed JSON
            return true.ToProperty();
        }
        catch (Exception)
        {
            // Any other exception means the system didn't handle the error gracefully
            return false.ToProperty();
        }
    }

    /// <summary>
    /// Property test to verify that invalid configurations are properly rejected by validation.
    /// </summary>
    [Property(Arbitrary = new[] { typeof(ConfigurationPropertyTests) })]
    public Property InvalidConfigurationsAreRejectedByValidation(TimelineConfiguration invalidConfig)
    {
        try
        {
            var configService = new Jellyfin.Plugin.TimelineManager.Services.ConfigurationService(
                Microsoft.Extensions.Logging.Abstractions.NullLogger<Jellyfin.Plugin.TimelineManager.Services.ConfigurationService>.Instance);
            
            var validationResult = configService.ValidateConfiguration(invalidConfig);
            
            // Invalid configurations should always fail validation
            var isProperlyRejected = !validationResult.IsValid && validationResult.Errors.Count > 0;
            
            // Validation should provide descriptive error messages
            var hasDescriptiveErrors = validationResult.Errors.All(error => 
                !string.IsNullOrWhiteSpace(error) && error.Length > 5);
            
            return (isProperlyRejected && hasDescriptiveErrors).ToProperty();
        }
        catch (Exception)
        {
            // Validation should not throw exceptions, it should return error results
            return false.ToProperty();
        }
    }

    /// <summary>
    /// Property test to verify that the system handles null configurations gracefully.
    /// </summary>
    [Property]
    public Property NullConfigurationIsHandledGracefully()
    {
        try
        {
            var configService = new Jellyfin.Plugin.TimelineManager.Services.ConfigurationService(
                Microsoft.Extensions.Logging.Abstractions.NullLogger<Jellyfin.Plugin.TimelineManager.Services.ConfigurationService>.Instance);
            
            var validationResult = configService.ValidateConfiguration(null!);
            
            // Null configuration should be rejected with appropriate error
            var isProperlyRejected = !validationResult.IsValid && 
                                   validationResult.Errors.Count > 0 &&
                                   validationResult.Errors.Any(e => e.Contains("null", StringComparison.OrdinalIgnoreCase));
            
            return isProperlyRejected.ToProperty();
        }
        catch (Exception)
        {
            // Should not throw exceptions for null input
            return false.ToProperty();
        }
    }
}