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
/// Service for managing individual universe files (CRUD operations).
/// </summary>
public class UniverseManagementService
{
    private readonly ILogger<UniverseManagementService> _logger;
    private readonly string _universesDirectoryPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="UniverseManagementService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="universesDirectoryPath">The path to the universes directory.</param>
    public UniverseManagementService(
        ILogger<UniverseManagementService> logger,
        string universesDirectoryPath = "/config/universes")
    {
        _logger = logger;
        _universesDirectoryPath = universesDirectoryPath;
    }

    /// <summary>
    /// Ensures the universes directory exists, creating it if necessary.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task EnsureUniversesDirectoryExistsAsync()
    {
        try
        {
            if (!Directory.Exists(_universesDirectoryPath))
            {
                _logger.LogInformation("Creating universes directory at {DirectoryPath}", _universesDirectoryPath);
                Directory.CreateDirectory(_universesDirectoryPath);
                _logger.LogInformation("Successfully created universes directory");
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied when creating universes directory at {DirectoryPath}", _universesDirectoryPath);
            throw;
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "I/O error creating universes directory at {DirectoryPath}", _universesDirectoryPath);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating universes directory at {DirectoryPath}", _universesDirectoryPath);
            throw;
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Gets metadata for all universe files in the universes directory.
    /// </summary>
    /// <returns>A list of universe metadata objects.</returns>
    public async Task<List<UniverseMetadata>> GetAllUniversesAsync()
    {
        var universes = new List<UniverseMetadata>();

        try
        {
            await EnsureUniversesDirectoryExistsAsync();

            if (!Directory.Exists(_universesDirectoryPath))
            {
                _logger.LogWarning("Universes directory does not exist at {DirectoryPath}", _universesDirectoryPath);
                return universes;
            }

            var jsonFiles = Directory.GetFiles(_universesDirectoryPath, "*.json");
            _logger.LogInformation("Found {FileCount} universe files in {DirectoryPath}", jsonFiles.Length, _universesDirectoryPath);

            foreach (var filePath in jsonFiles)
            {
                try
                {
                    var filename = Path.GetFileName(filePath);
                    var universe = await GetUniverseAsync(filename);

                    if (universe != null)
                    {
                        universes.Add(new UniverseMetadata
                        {
                            Key = universe.Key,
                            Name = universe.Name,
                            Filename = filename
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading universe file {FilePath}, skipping", filePath);
                }
            }

            _logger.LogInformation("Successfully loaded {UniverseCount} universes", universes.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning universes directory at {DirectoryPath}", _universesDirectoryPath);
        }

        return universes;
    }

    /// <summary>
    /// Gets the full content of a specific universe file.
    /// </summary>
    /// <param name="filename">The filename of the universe (e.g., "mcu.json").</param>
    /// <returns>The universe object, or null if not found or invalid.</returns>
    public async Task<Universe?> GetUniverseAsync(string filename)
    {
        try
        {
            var filePath = Path.Combine(_universesDirectoryPath, filename);

            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Universe file not found: {FilePath}", filePath);
                return null;
            }

            var jsonContent = await File.ReadAllTextAsync(filePath);

            if (string.IsNullOrWhiteSpace(jsonContent))
            {
                _logger.LogError("Universe file is empty: {FilePath}", filePath);
                return null;
            }

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                PropertyNameCaseInsensitive = true
            };

            var universe = JsonSerializer.Deserialize<Universe>(jsonContent, options);

            if (universe == null)
            {
                _logger.LogError("Failed to deserialize universe file: {FilePath}", filePath);
                return null;
            }

            _logger.LogDebug("Successfully loaded universe '{UniverseName}' from {FilePath}", universe.Name, filePath);
            return universe;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parsing error in universe file {Filename}", filename);
            return null;
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "I/O error reading universe file {Filename}", filename);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error loading universe file {Filename}", filename);
            return null;
        }
    }

    /// <summary>
    /// Saves a universe to a file (create or update).
    /// </summary>
    /// <param name="filename">The filename to save to (e.g., "mcu.json").</param>
    /// <param name="universe">The universe object to save.</param>
    /// <returns>A result indicating success or failure with error details.</returns>
    public async Task<SaveUniverseResult> SaveUniverseAsync(string filename, Universe universe)
    {
        var result = new SaveUniverseResult();

        try
        {
            // Validate universe structure
            var validationResult = ValidateUniverse(universe);
            if (!validationResult.IsValid)
            {
                result.Success = false;
                result.Errors = validationResult.Errors;
                _logger.LogWarning("Universe validation failed for {Filename}: {Errors}", 
                    filename, string.Join(", ", result.Errors));
                return result;
            }

            await EnsureUniversesDirectoryExistsAsync();

            var filePath = Path.Combine(_universesDirectoryPath, filename);
            var tempFilePath = filePath + ".tmp";

            // Serialize to JSON with indentation
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };

            var jsonContent = JsonSerializer.Serialize(universe, options);

            // Atomic write: write to temp file, then rename
            await File.WriteAllTextAsync(tempFilePath, jsonContent);
            
            // Replace existing file with temp file
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            File.Move(tempFilePath, filePath);

            result.Success = true;
            _logger.LogInformation("Successfully saved universe '{UniverseName}' to {FilePath}", universe.Name, filePath);
        }
        catch (UnauthorizedAccessException ex)
        {
            result.Success = false;
            result.Errors.Add($"Access denied: {ex.Message}");
            _logger.LogError(ex, "Access denied saving universe file {Filename}", filename);
        }
        catch (IOException ex)
        {
            result.Success = false;
            result.Errors.Add($"I/O error: {ex.Message}");
            _logger.LogError(ex, "I/O error saving universe file {Filename}", filename);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Errors.Add($"Unexpected error: {ex.Message}");
            _logger.LogError(ex, "Unexpected error saving universe file {Filename}", filename);
        }

        return result;
    }

    /// <summary>
    /// Deletes a universe file.
    /// </summary>
    /// <param name="filename">The filename to delete (e.g., "mcu.json").</param>
    /// <returns>True if deletion succeeded, false otherwise.</returns>
    public async Task<bool> DeleteUniverseAsync(string filename)
    {
        try
        {
            var filePath = Path.Combine(_universesDirectoryPath, filename);

            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Cannot delete universe file - file not found: {FilePath}", filePath);
                return false;
            }

            File.Delete(filePath);
            _logger.LogInformation("Successfully deleted universe file: {FilePath}", filePath);
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied deleting universe file {Filename}", filename);
            return false;
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "I/O error deleting universe file {Filename}", filename);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error deleting universe file {Filename}", filename);
            return false;
        }
    }

    /// <summary>
    /// Validates a universe object structure.
    /// </summary>
    /// <param name="universe">The universe to validate.</param>
    /// <returns>A validation result with success status and error details.</returns>
    public ConfigurationValidationResult ValidateUniverse(Universe universe)
    {
        var errors = new List<string>();

        if (universe == null)
        {
            errors.Add("Universe is null");
            return new ConfigurationValidationResult { IsValid = false, Errors = errors };
        }

        // Validate using data annotations
        var validationContext = new ValidationContext(universe);
        var validationResults = new List<ValidationResult>();
        
        if (!Validator.TryValidateObject(universe, validationContext, validationResults, true))
        {
            errors.AddRange(validationResults.Select(vr => vr.ErrorMessage ?? "Unknown validation error"));
        }

        // Additional business rule validations
        if (string.IsNullOrWhiteSpace(universe.Key))
        {
            errors.Add("Universe key cannot be empty");
        }
        else if (universe.Key.Contains(' '))
        {
            errors.Add($"Universe key '{universe.Key}' cannot contain spaces");
        }

        if (string.IsNullOrWhiteSpace(universe.Name))
        {
            errors.Add("Universe name cannot be empty");
        }

        if (universe.Items == null)
        {
            errors.Add("Universe items collection cannot be null");
        }
        else if (!universe.Items.GetType().IsArray && !typeof(System.Collections.IList).IsAssignableFrom(universe.Items.GetType()))
        {
            errors.Add("Universe items must be an array or list");
        }

        return new ConfigurationValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }
}
