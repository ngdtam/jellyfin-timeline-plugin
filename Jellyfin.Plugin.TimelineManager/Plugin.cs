using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Jellyfin.Plugin.TimelineManager.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TimelineManager;

/// <summary>
/// The main plugin class for the Universal Timeline Manager.
/// Handles plugin initialization, service registration, and dependency injection.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    private readonly ILogger<Plugin> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{Plugin}"/> interface.</param>
    public Plugin(
        IApplicationPaths applicationPaths, 
        IXmlSerializer xmlSerializer,
        ILogger<Plugin> logger)
        : base(applicationPaths, xmlSerializer)
    {
        _logger = logger;
        Instance = this;
        
        _logger.LogInformation("Universal Timeline Manager plugin initialized with ID: {PluginId}", Id);
        _logger.LogDebug("Plugin instance created at {Timestamp}", DateTime.UtcNow);
        
        // Run migration and initialization asynchronously
        _ = Task.Run(async () => await InitializePluginAsync());
    }
    
    /// <summary>
    /// Initializes the plugin asynchronously, including migration and default configuration.
    /// </summary>
    private async Task InitializePluginAsync()
    {
        try
        {
            _logger.LogInformation("Starting plugin initialization");
            
            // Initialize services
            var universeManagementLogger = LoggerFactory.Create(builder => builder.AddConsole())
                .CreateLogger<UniverseManagementService>();
            var universeManagementService = new UniverseManagementService(universeManagementLogger);
            
            var migrationLogger = LoggerFactory.Create(builder => builder.AddConsole())
                .CreateLogger<MigrationService>();
            var migrationService = new MigrationService(migrationLogger, universeManagementService);
            
            // Check if migration is needed
            var migrationNeeded = await migrationService.IsMigrationNeededAsync();
            
            if (migrationNeeded)
            {
                _logger.LogInformation("Legacy configuration detected - starting migration to multi-file format");
                var migrationResult = await migrationService.MigrateAsync();
                
                if (migrationResult.Success)
                {
                    _logger.LogInformation("Migration completed successfully - migrated {Count} universes", 
                        migrationResult.UniversesMigrated);
                    if (migrationResult.BackupCreated)
                    {
                        _logger.LogInformation("Legacy configuration backed up successfully");
                    }
                }
                else
                {
                    _logger.LogError("Migration failed: {Errors}", string.Join(", ", migrationResult.Errors));
                }
            }
            else
            {
                _logger.LogDebug("No migration needed");
            }
            
            // Check if universes directory is empty and create example if needed
            var universes = await universeManagementService.GetAllUniversesAsync();
            if (universes.Count == 0)
            {
                _logger.LogInformation("No universe files found - creating example universe");
                await migrationService.CreateExampleUniverseAsync();
            }
            else
            {
                _logger.LogInformation("Found {Count} universe files", universes.Count);
            }
            
            _logger.LogInformation("Plugin initialization completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during plugin initialization");
        }
    }
    
    /// <summary>
    /// Creates a default configuration file if one doesn't exist (legacy method - kept for backward compatibility).
    /// </summary>
    [Obsolete("This method is deprecated. Migration and initialization now handled in InitializePluginAsync.")]
    private void InitializeDefaultConfiguration()
    {
        try
        {
            var configPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "jellyfin",
                "config",
                "timeline_manager_config.json"
            );
            
            // Check if config file already exists
            if (System.IO.File.Exists(configPath))
            {
                _logger.LogDebug("Configuration file already exists at {ConfigPath}", configPath);
                return;
            }
            
            _logger.LogInformation("Configuration file not found. Creating default configuration at {ConfigPath}", configPath);
            
            // Ensure directory exists
            var directory = System.IO.Path.GetDirectoryName(configPath);
            if (!string.IsNullOrEmpty(directory) && !System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
                _logger.LogDebug("Created configuration directory: {Directory}", directory);
            }
            
            // Read embedded default config
            var assembly = GetType().Assembly;
            var resourceName = "Jellyfin.Plugin.TimelineManager.Configuration.default_config.json";
            
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                _logger.LogWarning("Could not find embedded default configuration resource");
                
                // Fallback: Create a minimal config
                var fallbackConfig = @"{
  ""universes"": [
    {
      ""key"": ""example"",
      ""name"": ""Example Timeline"",
      ""items"": [
        {
          ""providerId"": ""1771"",
          ""providerName"": ""tmdb"",
          ""type"": ""movie""
        }
      ]
    }
  ]
}";
                System.IO.File.WriteAllText(configPath, fallbackConfig);
                _logger.LogInformation("Created fallback configuration file");
                return;
            }
            
            // Copy embedded config to file system
            using var reader = new System.IO.StreamReader(stream);
            var configContent = reader.ReadToEnd();
            System.IO.File.WriteAllText(configPath, configContent);
            
            _logger.LogInformation("Successfully created default configuration file with example timeline");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create default configuration file. Users will need to create it manually.");
        }
    }

    /// <inheritdoc />
    public override string Name => "Universal Timeline Manager";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("12345678-1234-5678-9abc-123456789012");

    /// <inheritdoc />
    public override string Description => "Creates and maintains chronological playlists for cinematic universes based on JSON configuration files.";

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.configPage.html"
            },
            new PluginPageInfo
            {
                Name = "configPage.js",
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.configPage.js"
            }
        };
    }
}
