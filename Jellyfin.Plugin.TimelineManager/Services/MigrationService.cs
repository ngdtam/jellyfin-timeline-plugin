using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Plugin.TimelineManager.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TimelineManager.Services;

/// <summary>
/// Service for migrating legacy single-file configuration to multi-file universe format.
/// </summary>
public class MigrationService
{
    private readonly ILogger<MigrationService> _logger;
    private readonly UniverseManagementService _universeManagementService;
    private readonly string _legacyConfigPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="MigrationService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="universeManagementService">The universe management service.</param>
    /// <param name="legacyConfigPath">The path to the legacy configuration file.</param>
    public MigrationService(
        ILogger<MigrationService> logger,
        UniverseManagementService universeManagementService,
        string legacyConfigPath = "/config/timeline_manager_config.json")
    {
        _logger = logger;
        _universeManagementService = universeManagementService;
        _legacyConfigPath = legacyConfigPath;
    }

    /// <summary>
    /// Checks if migration is needed.
    /// </summary>
    /// <returns>True if migration should run, false otherwise.</returns>
    public async Task<bool> IsMigrationNeededAsync()
    {
        try
        {
            // Check if legacy config exists
            if (!File.Exists(_legacyConfigPath))
            {
                _logger.LogDebug("No legacy configuration file found at {LegacyConfigPath}", _legacyConfigPath);
                return false;
            }

            // Check if backup already exists (migration already done)
            var backupPath = _legacyConfigPath + ".backup";
            if (File.Exists(backupPath))
            {
                _logger.LogInformation("Migration already completed - backup file exists at {BackupPath}", backupPath);
                return false;
            }

            _logger.LogInformation("Migration needed - legacy config exists and no backup found");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if migration is needed");
            return false;
        }
    }

    /// <summary>
    /// Performs migration from legacy single-file to multi-file format.
    /// </summary>
    /// <returns>A migration result with detailed status.</returns>
    public async Task<MigrationResult> MigrateAsync()
    {
        var result = new MigrationResult
        {
            Success = false,
            UniversesMigrated = 0,
            BackupCreated = false,
            Errors = new List<string>()
        };

        try
        {
            _logger.LogInformation("Starting migration from legacy configuration format");

            // Validate legacy config exists
            if (!File.Exists(_legacyConfigPath))
            {
                result.Errors.Add("Legacy configuration file not found");
                _logger.LogWarning("Migration aborted - legacy config not found at {LegacyConfigPath}", _legacyConfigPath);
                return result;
            }

            // Read and parse legacy configuration
            _logger.LogInformation("Reading legacy configuration from {LegacyConfigPath}", _legacyConfigPath);
            var jsonContent = await File.ReadAllTextAsync(_legacyConfigPath);

            if (string.IsNullOrWhiteSpace(jsonContent))
            {
                result.Errors.Add("Legacy configuration file is empty");
                _logger.LogError("Migration aborted - legacy config is empty");
                return result;
            }

            TimelineConfiguration? legacyConfig;
            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    PropertyNameCaseInsensitive = true
                };

                legacyConfig = JsonSerializer.Deserialize<TimelineConfiguration>(jsonContent, options);

                if (legacyConfig == null)
                {
                    result.Errors.Add("Failed to parse legacy configuration - deserialization returned null");
                    _logger.LogError("Migration aborted - failed to deserialize legacy config");
                    return result;
                }
            }
            catch (JsonException ex)
            {
                result.Errors.Add($"Invalid JSON in legacy configuration: {ex.Message}");
                _logger.LogError(ex, "Migration aborted - invalid JSON in legacy config");
                return result;
            }

            // Validate legacy config has universes
            if (legacyConfig.Universes == null || legacyConfig.Universes.Count == 0)
            {
                result.Errors.Add("Legacy configuration has no universes to migrate");
                _logger.LogWarning("Migration aborted - no universes found in legacy config");
                return result;
            }

            _logger.LogInformation("Found {UniverseCount} universes to migrate", legacyConfig.Universes.Count);

            // Ensure universes directory exists
            await _universeManagementService.EnsureUniversesDirectoryExistsAsync();

            // Migrate each universe to individual file
            var migratedCount = 0;
            var migrationErrors = new List<string>();

            foreach (var universe in legacyConfig.Universes)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(universe.Key))
                    {
                        migrationErrors.Add("Skipped universe with empty key");
                        _logger.LogWarning("Skipping universe with empty key");
                        continue;
                    }

                    var filename = $"{universe.Key}.json";
                    _logger.LogInformation("Migrating universe '{UniverseName}' to {Filename}", universe.Name, filename);

                    var saveResult = await _universeManagementService.SaveUniverseAsync(filename, universe);

                    if (saveResult.Success)
                    {
                        migratedCount++;
                        _logger.LogInformation("Successfully migrated universe '{UniverseName}'", universe.Name);
                    }
                    else
                    {
                        var errorMsg = $"Failed to migrate universe '{universe.Name}': {string.Join(", ", saveResult.Errors)}";
                        migrationErrors.Add(errorMsg);
                        _logger.LogError("Failed to migrate universe '{UniverseName}': {Errors}", 
                            universe.Name, string.Join(", ", saveResult.Errors));
                    }
                }
                catch (Exception ex)
                {
                    var errorMsg = $"Error migrating universe '{universe.Name}': {ex.Message}";
                    migrationErrors.Add(errorMsg);
                    _logger.LogError(ex, "Error migrating universe '{UniverseName}'", universe.Name);
                }
            }

            result.UniversesMigrated = migratedCount;
            result.Errors.AddRange(migrationErrors);

            // Only create backup if at least one universe was migrated successfully
            if (migratedCount > 0)
            {
                try
                {
                    await BackupLegacyConfigAsync();
                    result.BackupCreated = true;
                    _logger.LogInformation("Successfully created backup of legacy configuration");
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Failed to create backup: {ex.Message}");
                    _logger.LogError(ex, "Failed to create backup of legacy configuration");
                }
            }

            // Migration is successful if at least one universe was migrated
            result.Success = migratedCount > 0;

            if (result.Success)
            {
                _logger.LogInformation("Migration completed successfully - migrated {MigratedCount} of {TotalCount} universes",
                    migratedCount, legacyConfig.Universes.Count);
            }
            else
            {
                _logger.LogError("Migration failed - no universes were migrated successfully");
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Unexpected migration error: {ex.Message}");
            _logger.LogError(ex, "Unexpected error during migration");
        }

        return result;
    }

    /// <summary>
    /// Creates an example universe file for new installations.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task CreateExampleUniverseAsync()
    {
        try
        {
            _logger.LogInformation("Creating example universe file");

            await _universeManagementService.EnsureUniversesDirectoryExistsAsync();

            var exampleUniverse = new Universe
            {
                Key = "example",
                Name = "Example Universe",
                Items = new List<TimelineItem>
                {
                    new TimelineItem
                    {
                        ProviderId = "1771",
                        ProviderName = "tmdb",
                        Type = "movie"
                    },
                    new TimelineItem
                    {
                        ProviderId = "1726",
                        ProviderName = "tmdb",
                        Type = "movie"
                    }
                }
            };

            var result = await _universeManagementService.SaveUniverseAsync("example.json", exampleUniverse);

            if (result.Success)
            {
                _logger.LogInformation("Successfully created example universe file");
            }
            else
            {
                _logger.LogError("Failed to create example universe: {Errors}", string.Join(", ", result.Errors));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating example universe file");
        }
    }

    /// <summary>
    /// Creates a backup of the legacy configuration file.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task BackupLegacyConfigAsync()
    {
        try
        {
            var backupPath = _legacyConfigPath + ".backup";

            if (File.Exists(backupPath))
            {
                _logger.LogWarning("Backup file already exists at {BackupPath}, will overwrite", backupPath);
                File.Delete(backupPath);
            }

            _logger.LogInformation("Creating backup of legacy configuration at {BackupPath}", backupPath);
            File.Move(_legacyConfigPath, backupPath);
            _logger.LogInformation("Successfully backed up legacy configuration");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to backup legacy configuration");
            throw;
        }

        await Task.CompletedTask;
    }
}
