using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Plugin.TimelineManager.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TimelineManager.Services;

/// <summary>
/// Service for loading and validating timeline configuration from JSON files.
/// </summary>
public class ConfigurationService
{
    private readonly ILogger<ConfigurationService> _logger;
    private readonly string _configurationPath;

    private readonly UniverseManagementService? _universeManagementService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="configurationPath">The path to the configuration file.</param>
    /// <param name="universeManagementService">Optional universe management service for multi-file support.</param>
    public ConfigurationService(
        ILogger<ConfigurationService> logger, 
        string configurationPath = "/config/timeline_manager_config.json",
        UniverseManagementService? universeManagementService = null)
    {
        _logger = logger;
        _configurationPath = configurationPath;
        _universeManagementService = universeManagementService;
    }

    /// <summary>
    /// Loads and validates the timeline configuration from the JSON file.
    /// </summary>
    /// <returns>The loaded and validated configuration, or null if loading failed.</returns>
    public async Task<TimelineConfiguration?> LoadConfigurationAsync()
    {
        try
        {
            _logger.LogInformation("Loading timeline configuration from {ConfigPath}", _configurationPath);

            // Enhanced file existence and accessibility checks
            var fileCheckResult = await ValidateConfigurationFileAsync();
            if (!fileCheckResult.IsValid)
            {
                foreach (var error in fileCheckResult.Errors)
                {
                    _logger.LogError("Configuration file validation failed: {Error}", error);
                }
                return null;
            }

            var jsonContent = await File.ReadAllTextAsync(_configurationPath);
            
            if (string.IsNullOrWhiteSpace(jsonContent))
            {
                _logger.LogError("Configuration file is empty at {ConfigPath}", _configurationPath);
                return null;
            }

            // Enhanced JSON parsing with detailed error reporting
            var configuration = await ParseJsonConfigurationAsync(jsonContent);
            if (configuration == null)
            {
                return null;
            }

            var validationResult = ValidateConfiguration(configuration);
            if (!validationResult.IsValid)
            {
                _logger.LogError("Configuration validation failed with {ErrorCount} errors:", validationResult.Errors.Count);
                foreach (var error in validationResult.Errors)
                {
                    _logger.LogError("  - {ValidationError}", error);
                }
                
                // Provide troubleshooting guidance
                LogTroubleshootingGuidance(validationResult.Errors);
                return null;
            }

            _logger.LogInformation("Successfully loaded configuration with {UniverseCount} universes", 
                configuration.Universes.Count);

            return configuration;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied to configuration file {ConfigPath}. Check file permissions.", _configurationPath);
            return null;
        }
        catch (DirectoryNotFoundException ex)
        {
            _logger.LogError(ex, "Configuration directory not found for {ConfigPath}. Ensure the directory exists.", _configurationPath);
            return null;
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogError(ex, "Configuration file not found at {ConfigPath}. Create the file with valid JSON configuration.", _configurationPath);
            LogSampleConfiguration();
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parsing error in configuration file {ConfigPath}", _configurationPath);
            LogJsonParsingGuidance(ex);
            return null;
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "I/O error reading configuration file {ConfigPath}: {Message}", _configurationPath, ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error loading configuration from {ConfigPath}: {Message}", _configurationPath, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Validates that the configuration file exists and is accessible.
    /// </summary>
    /// <returns>A validation result indicating file accessibility.</returns>
    private async Task<ConfigurationValidationResult> ValidateConfigurationFileAsync()
    {
        var errors = new List<string>();

        try
        {
            if (string.IsNullOrWhiteSpace(_configurationPath))
            {
                errors.Add("Configuration path is null or empty");
                return new ConfigurationValidationResult { IsValid = false, Errors = errors };
            }

            if (!File.Exists(_configurationPath))
            {
                errors.Add($"Configuration file not found at '{_configurationPath}'");
                
                // Check if directory exists
                var directory = Path.GetDirectoryName(_configurationPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    errors.Add($"Configuration directory '{directory}' does not exist");
                }
                
                return new ConfigurationValidationResult { IsValid = false, Errors = errors };
            }

            // Check file accessibility
            var fileInfo = new FileInfo(_configurationPath);
            if (fileInfo.Length == 0)
            {
                errors.Add("Configuration file is empty");
            }

            // Test read access
            try
            {
                using var stream = File.OpenRead(_configurationPath);
                // File is readable
            }
            catch (UnauthorizedAccessException)
            {
                errors.Add("Access denied to configuration file - check file permissions");
            }

            // Validate file extension
            var extension = Path.GetExtension(_configurationPath);
            if (!string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Configuration file does not have .json extension: {Extension}", extension);
            }

            return new ConfigurationValidationResult { IsValid = errors.Count == 0, Errors = errors };
        }
        catch (Exception ex)
        {
            errors.Add($"Error validating configuration file: {ex.Message}");
            return new ConfigurationValidationResult { IsValid = false, Errors = errors };
        }
    }

    /// <summary>
    /// Parses JSON configuration with enhanced error handling and reporting.
    /// </summary>
    /// <param name="jsonContent">The JSON content to parse.</param>
    /// <returns>The parsed configuration or null if parsing failed.</returns>
    private async Task<TimelineConfiguration?> ParseJsonConfigurationAsync(string jsonContent)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                PropertyNameCaseInsensitive = true
            };

            var configuration = JsonSerializer.Deserialize<TimelineConfiguration>(jsonContent, options);

            if (configuration == null)
            {
                _logger.LogError("JSON deserialization returned null - check JSON structure");
                LogJsonStructureGuidance();
                return null;
            }

            return configuration;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parsing failed: {Message}", ex.Message);
            
            // Provide detailed parsing error information
            if (ex.LineNumber.HasValue && ex.BytePositionInLine.HasValue)
            {
                _logger.LogError("JSON error at line {LineNumber}, position {Position}", 
                    ex.LineNumber.Value, ex.BytePositionInLine.Value);
                
                // Try to show the problematic line
                await LogProblematicJsonLineAsync(jsonContent, ex.LineNumber.Value);
            }
            
            return null;
        }
    }

    /// <summary>
    /// Logs the problematic line from JSON content for debugging.
    /// </summary>
    /// <param name="jsonContent">The JSON content.</param>
    /// <param name="lineNumber">The line number with the error.</param>
    private async Task LogProblematicJsonLineAsync(string jsonContent, long lineNumber)
    {
        try
        {
            var lines = jsonContent.Split('\n');
            if (lineNumber > 0 && lineNumber <= lines.Length)
            {
                var problematicLine = lines[lineNumber - 1];
                _logger.LogError("Problematic JSON line {LineNumber}: {LineContent}", lineNumber, problematicLine.Trim());
                
                // Show context lines if available
                if (lineNumber > 1)
                {
                    _logger.LogDebug("Previous line {LineNumber}: {LineContent}", lineNumber - 1, lines[lineNumber - 2].Trim());
                }
                if (lineNumber < lines.Length)
                {
                    _logger.LogDebug("Next line {LineNumber}: {LineContent}", lineNumber + 1, lines[lineNumber].Trim());
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not extract problematic JSON line");
        }
    }

    /// <summary>
    /// Logs troubleshooting guidance based on validation errors.
    /// </summary>
    /// <param name="errors">The validation errors.</param>
    private void LogTroubleshootingGuidance(List<string> errors)
    {
        _logger.LogInformation("Configuration Troubleshooting Guide:");
        
        if (errors.Any(e => e.Contains("null", StringComparison.OrdinalIgnoreCase)))
        {
            _logger.LogInformation("- Ensure all required fields are present and not null");
        }
        
        if (errors.Any(e => e.Contains("empty", StringComparison.OrdinalIgnoreCase)))
        {
            _logger.LogInformation("- Check that all string fields have valid values");
        }
        
        if (errors.Any(e => e.Contains("duplicate", StringComparison.OrdinalIgnoreCase)))
        {
            _logger.LogInformation("- Ensure all universe keys are unique");
        }
        
        if (errors.Any(e => e.Contains("provider", StringComparison.OrdinalIgnoreCase)))
        {
            _logger.LogInformation("- Valid providers: 'tmdb', 'imdb'");
        }
        
        if (errors.Any(e => e.Contains("type", StringComparison.OrdinalIgnoreCase)))
        {
            _logger.LogInformation("- Valid content types: 'movie', 'episode'");
        }
        
        _logger.LogInformation("- Check the sample configuration format in the logs above");
    }

    /// <summary>
    /// Logs JSON parsing guidance for common issues.
    /// </summary>
    /// <param name="jsonException">The JSON parsing exception.</param>
    private void LogJsonParsingGuidance(JsonException jsonException)
    {
        _logger.LogInformation("JSON Parsing Troubleshooting:");
        
        var message = jsonException.Message.ToLowerInvariant();
        
        if (message.Contains("unexpected character"))
        {
            _logger.LogInformation("- Check for invalid characters or missing quotes around strings");
        }
        
        if (message.Contains("unterminated string"))
        {
            _logger.LogInformation("- Ensure all strings are properly closed with quotes");
        }
        
        if (message.Contains("expected") && message.Contains("comma"))
        {
            _logger.LogInformation("- Check for missing commas between array elements or object properties");
        }
        
        if (message.Contains("trailing comma"))
        {
            _logger.LogInformation("- Remove trailing commas after the last element in arrays or objects");
        }
        
        _logger.LogInformation("- Validate JSON syntax using an online JSON validator");
        _logger.LogInformation("- Ensure proper nesting of objects and arrays");
    }

    /// <summary>
    /// Logs JSON structure guidance for proper configuration format.
    /// </summary>
    private void LogJsonStructureGuidance()
    {
        _logger.LogInformation("Expected JSON structure:");
        _logger.LogInformation("- Root object must contain 'universes' array");
        _logger.LogInformation("- Each universe must have 'key', 'name', and 'items' properties");
        _logger.LogInformation("- Each item must have 'providerId', 'providerName', and 'type' properties");
    }

    /// <summary>
    /// Logs a sample configuration for reference.
    /// </summary>
    private void LogSampleConfiguration()
    {
        _logger.LogInformation("Sample configuration file content:");
        _logger.LogInformation("""
            {
              "universes": [
                {
                  "key": "mcu",
                  "name": "Marvel Cinematic Universe",
                  "items": [
                    {
                      "providerId": "1771",
                      "providerName": "tmdb",
                      "type": "movie"
                    },
                    {
                      "providerId": "tt0371746",
                      "providerName": "imdb",
                      "type": "movie"
                    }
                  ]
                }
              ]
            }
            """);
    }

    /// <summary>
    /// Validates the loaded configuration against business rules and data annotations.
    /// </summary>
    /// <param name="configuration">The configuration to validate.</param>
    /// <returns>A validation result indicating success or failure with error details.</returns>
    public ConfigurationValidationResult ValidateConfiguration(TimelineConfiguration configuration)
    {
        var errors = new List<string>();

        if (configuration == null)
        {
            errors.Add("Configuration is null");
            return new ConfigurationValidationResult { IsValid = false, Errors = errors };
        }

        // Validate using data annotations
        var validationContext = new ValidationContext(configuration);
        var validationResults = new List<ValidationResult>();
        
        if (!Validator.TryValidateObject(configuration, validationContext, validationResults, true))
        {
            errors.AddRange(validationResults.Select(vr => vr.ErrorMessage ?? "Unknown validation error"));
        }

        // Validate universes collection
        if (configuration.Universes == null)
        {
            errors.Add("Universes collection is null");
        }
        else if (configuration.Universes.Count == 0)
        {
            errors.Add("At least one universe must be configured");
        }
        else
        {
            // Validate each universe
            for (int i = 0; i < configuration.Universes.Count; i++)
            {
                var universe = configuration.Universes[i];
                var universeErrors = ValidateUniverse(universe, i);
                errors.AddRange(universeErrors);
            }

            // Check for duplicate universe keys
            var duplicateKeys = configuration.Universes
                .GroupBy(u => u.Key, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);

            foreach (var duplicateKey in duplicateKeys)
            {
                errors.Add($"Duplicate universe key found: '{duplicateKey}'");
            }
        }

        return new ConfigurationValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }

    /// <summary>
    /// Validates a single universe configuration.
    /// </summary>
    /// <param name="universe">The universe to validate.</param>
    /// <param name="index">The index of the universe in the collection.</param>
    /// <returns>A list of validation errors for this universe.</returns>
    private List<string> ValidateUniverse(Universe universe, int index)
    {
        var errors = new List<string>();
        var prefix = $"Universe[{index}]";

        if (universe == null)
        {
            errors.Add($"{prefix}: Universe is null");
            return errors;
        }

        // Validate using data annotations
        var validationContext = new ValidationContext(universe);
        var validationResults = new List<ValidationResult>();
        
        if (!Validator.TryValidateObject(universe, validationContext, validationResults, true))
        {
            errors.AddRange(validationResults.Select(vr => $"{prefix}: {vr.ErrorMessage}"));
        }

        // Additional business rule validations
        if (string.IsNullOrWhiteSpace(universe.Key))
        {
            errors.Add($"{prefix}: Key cannot be empty");
        }
        else if (universe.Key.Contains(' '))
        {
            errors.Add($"{prefix}: Key '{universe.Key}' cannot contain spaces");
        }

        if (string.IsNullOrWhiteSpace(universe.Name))
        {
            errors.Add($"{prefix}: Name cannot be empty");
        }

        if (universe.Items == null)
        {
            errors.Add($"{prefix}: Items collection is null");
        }
        else if (universe.Items.Count == 0)
        {
            _logger.LogWarning("{Prefix}: Universe '{UniverseName}' has no items configured", prefix, universe.Name);
        }
        else
        {
            // Validate each timeline item
            for (int i = 0; i < universe.Items.Count; i++)
            {
                var item = universe.Items[i];
                var itemErrors = ValidateTimelineItem(item, index, i);
                errors.AddRange(itemErrors);
            }
        }

        return errors;
    }

    /// <summary>
    /// Validates a single timeline item configuration.
    /// </summary>
    /// <param name="item">The timeline item to validate.</param>
    /// <param name="universeIndex">The index of the parent universe.</param>
    /// <param name="itemIndex">The index of the item within the universe.</param>
    /// <returns>A list of validation errors for this timeline item.</returns>
    private List<string> ValidateTimelineItem(TimelineItem item, int universeIndex, int itemIndex)
    {
        var errors = new List<string>();
        var prefix = $"Universe[{universeIndex}].Items[{itemIndex}]";

        if (item == null)
        {
            errors.Add($"{prefix}: Timeline item is null");
            return errors;
        }

        // Validate using data annotations
        var validationContext = new ValidationContext(item);
        var validationResults = new List<ValidationResult>();
        
        if (!Validator.TryValidateObject(item, validationContext, validationResults, true))
        {
            errors.AddRange(validationResults.Select(vr => $"{prefix}: {vr.ErrorMessage}"));
        }

        // Additional business rule validations
        if (string.IsNullOrWhiteSpace(item.ProviderId))
        {
            errors.Add($"{prefix}: ProviderId cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(item.ProviderName))
        {
            errors.Add($"{prefix}: ProviderName cannot be empty");
        }
        else if (!IsValidProviderName(item.ProviderName))
        {
            errors.Add($"{prefix}: ProviderName '{item.ProviderName}' is not supported. Valid values: tmdb, imdb");
        }

        if (string.IsNullOrWhiteSpace(item.Type))
        {
            errors.Add($"{prefix}: Type cannot be empty");
        }
        else if (!IsValidContentType(item.Type))
        {
            errors.Add($"{prefix}: Type '{item.Type}' is not supported. Valid values: movie, episode");
        }

        return errors;
    }

    /// <summary>
    /// Checks if the provider name is valid.
    /// </summary>
    /// <param name="providerName">The provider name to validate.</param>
    /// <returns>True if the provider name is valid, false otherwise.</returns>
    private static bool IsValidProviderName(string providerName)
    {
        var validProviders = new[] { "tmdb", "imdb" };
        return validProviders.Contains(providerName.ToLowerInvariant());
    }

    /// <summary>
    /// Checks if the content type is valid.
    /// </summary>
    /// <param name="contentType">The content type to validate.</param>
    /// <returns>True if the content type is valid, false otherwise.</returns>
    private static bool IsValidContentType(string contentType)
    {
        var validTypes = new[] { "movie", "episode" };
        return validTypes.Contains(contentType.ToLowerInvariant());
    }

    /// <summary>
    /// Loads configuration from selected universe files.
    /// </summary>
    /// <param name="selectedFilenames">List of universe filenames to load.</param>
    /// <returns>The merged configuration from selected universes, or null if loading failed.</returns>
    public async Task<TimelineConfiguration?> LoadFromUniverseFilesAsync(List<string> selectedFilenames)
    {
        try
        {
            if (_universeManagementService == null)
            {
                _logger.LogError("UniverseManagementService not available for multi-file loading");
                return null;
            }

            if (selectedFilenames == null || selectedFilenames.Count == 0)
            {
                _logger.LogWarning("No universe filenames provided, loading all universes");
                return await LoadAllUniversesAsync();
            }

            _logger.LogInformation("Loading {Count} selected universe files", selectedFilenames.Count);

            var universes = new List<Universe>();

            foreach (var filename in selectedFilenames)
            {
                var universe = await _universeManagementService.GetUniverseAsync(filename);
                if (universe != null)
                {
                    universes.Add(universe);
                    _logger.LogDebug("Loaded universe '{UniverseName}' from {Filename}", universe.Name, filename);
                }
                else
                {
                    _logger.LogWarning("Failed to load universe from {Filename}", filename);
                }
            }

            if (universes.Count == 0)
            {
                _logger.LogError("No universes were loaded successfully");
                return null;
            }

            var configuration = MergeUniverses(universes);
            _logger.LogInformation("Successfully loaded {Count} universes from selected files", universes.Count);

            return configuration;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading configuration from universe files");
            return null;
        }
    }

    /// <summary>
    /// Loads configuration from all available universe files.
    /// </summary>
    /// <returns>The merged configuration from all universes, or null if loading failed.</returns>
    public async Task<TimelineConfiguration?> LoadAllUniversesAsync()
    {
        try
        {
            if (_universeManagementService == null)
            {
                _logger.LogError("UniverseManagementService not available for multi-file loading");
                return null;
            }

            _logger.LogInformation("Loading all available universe files");

            var universeMetadata = await _universeManagementService.GetAllUniversesAsync();

            if (universeMetadata.Count == 0)
            {
                _logger.LogWarning("No universe files found");
                return new TimelineConfiguration { Universes = new List<Universe>() };
            }

            var universes = new List<Universe>();

            foreach (var metadata in universeMetadata)
            {
                var universe = await _universeManagementService.GetUniverseAsync(metadata.Filename);
                if (universe != null)
                {
                    universes.Add(universe);
                }
            }

            if (universes.Count == 0)
            {
                _logger.LogError("No universes were loaded successfully");
                return null;
            }

            var configuration = MergeUniverses(universes);
            _logger.LogInformation("Successfully loaded all {Count} universes", universes.Count);

            return configuration;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading all universe files");
            return null;
        }
    }

    /// <summary>
    /// Merges multiple universes into a single configuration.
    /// </summary>
    /// <param name="universes">The list of universes to merge.</param>
    /// <returns>A timeline configuration containing all universes.</returns>
    private TimelineConfiguration MergeUniverses(List<Universe> universes)
    {
        return new TimelineConfiguration
        {
            Universes = universes
        };
    }
}

/// <summary>
/// Result of configuration validation containing success status and error details.
/// </summary>
public class ConfigurationValidationResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the configuration is valid.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets the list of validation errors.
    /// </summary>
    public List<string> Errors { get; set; } = new();
}