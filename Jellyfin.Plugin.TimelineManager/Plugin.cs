using System;
using System.Collections.Generic;
using Jellyfin.Plugin.TimelineManager.Services;
using Jellyfin.Plugin.TimelineManager.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Playlists;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.DependencyInjection;
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

    /// <summary>
    /// Registers all plugin services with the dependency injection container.
    /// This method is called by Jellyfin during plugin initialization.
    /// </summary>
    /// <param name="serviceCollection">The service collection to register services with.</param>
    public void RegisterServices(IServiceCollection serviceCollection)
    {
        try
        {
            _logger.LogInformation("Registering Universal Timeline Manager services with dependency injection container");

            // Register the main timeline task as a scheduled task
            serviceCollection.AddSingleton<IScheduledTask, TimelineConfigTask>();
            _logger.LogDebug("Registered TimelineConfigTask as IScheduledTask");

            // Register core services with appropriate lifetimes
            serviceCollection.AddSingleton<ConfigurationService>(serviceProvider =>
            {
                var logger = serviceProvider.GetRequiredService<ILogger<ConfigurationService>>();
                return new ConfigurationService(logger);
            });
            _logger.LogDebug("Registered ConfigurationService as singleton");

            serviceCollection.AddSingleton<ContentLookupService>(serviceProvider =>
            {
                var logger = serviceProvider.GetRequiredService<ILogger<ContentLookupService>>();
                var libraryManager = serviceProvider.GetRequiredService<ILibraryManager>();
                return new ContentLookupService(logger, libraryManager);
            });
            _logger.LogDebug("Registered ContentLookupService as singleton");

            serviceCollection.AddSingleton<ProviderMatchingService>(serviceProvider =>
            {
                var logger = serviceProvider.GetRequiredService<ILogger<ProviderMatchingService>>();
                var contentLookupService = serviceProvider.GetRequiredService<ContentLookupService>();
                return new ProviderMatchingService(logger, contentLookupService);
            });
            _logger.LogDebug("Registered ProviderMatchingService as singleton");

            serviceCollection.AddSingleton<MixedContentService>(serviceProvider =>
            {
                var logger = serviceProvider.GetRequiredService<ILogger<MixedContentService>>();
                var providerMatchingService = serviceProvider.GetRequiredService<ProviderMatchingService>();
                return new MixedContentService(logger, providerMatchingService);
            });
            _logger.LogDebug("Registered MixedContentService as singleton");

            serviceCollection.AddSingleton<PlaylistService>(serviceProvider =>
            {
                var logger = serviceProvider.GetRequiredService<ILogger<PlaylistService>>();
                var playlistManager = serviceProvider.GetRequiredService<IPlaylistManager>();
                return new PlaylistService(logger, playlistManager);
            });
            _logger.LogDebug("Registered PlaylistService as singleton");

            serviceCollection.AddSingleton<MixedContentPlaylistService>(serviceProvider =>
            {
                var logger = serviceProvider.GetRequiredService<ILogger<MixedContentPlaylistService>>();
                var playlistService = serviceProvider.GetRequiredService<PlaylistService>();
                var mixedContentService = serviceProvider.GetRequiredService<MixedContentService>();
                return new MixedContentPlaylistService(logger, playlistService, mixedContentService);
            });
            _logger.LogDebug("Registered MixedContentPlaylistService as singleton");

            serviceCollection.AddSingleton<PlaylistErrorHandler>(serviceProvider =>
            {
                var logger = serviceProvider.GetRequiredService<ILogger<PlaylistErrorHandler>>();
                return new PlaylistErrorHandler(logger);
            });
            _logger.LogDebug("Registered PlaylistErrorHandler as singleton");

            _logger.LogInformation("Successfully registered all Universal Timeline Manager services");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register Universal Timeline Manager services: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Validates that all required services are properly registered and available.
    /// </summary>
    /// <param name="serviceProvider">The service provider to validate against.</param>
    /// <returns>True if all services are available, false otherwise.</returns>
    public bool ValidateServiceRegistration(IServiceProvider serviceProvider)
    {
        try
        {
            _logger.LogDebug("Validating service registration for Universal Timeline Manager");

            var requiredServices = new[]
            {
                typeof(IScheduledTask),
                typeof(ConfigurationService),
                typeof(ContentLookupService),
                typeof(ProviderMatchingService),
                typeof(MixedContentService),
                typeof(PlaylistService),
                typeof(MixedContentPlaylistService),
                typeof(PlaylistErrorHandler),
                typeof(ILibraryManager),
                typeof(IPlaylistManager)
            };

            foreach (var serviceType in requiredServices)
            {
                var service = serviceProvider.GetService(serviceType);
                if (service == null)
                {
                    _logger.LogError("Required service {ServiceType} is not registered or available", serviceType.Name);
                    return false;
                }
                _logger.LogDebug("Service {ServiceType} is properly registered and available", serviceType.Name);
            }

            _logger.LogInformation("All required services are properly registered and available");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Service validation failed: {Message}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Gets plugin health information for diagnostics.
    /// </summary>
    /// <returns>A dictionary containing plugin health metrics.</returns>
    public Dictionary<string, object> GetHealthMetrics()
    {
        var metrics = new Dictionary<string, object>
        {
            ["PluginName"] = Name,
            ["PluginId"] = Id.ToString(),
            ["Version"] = Version.ToString(),
            ["InitializationTime"] = DateTime.UtcNow,
            ["IsActive"] = Instance != null
        };

        try
        {
            // Add configuration status
            metrics["ConfigurationStatus"] = Configuration != null ? "Loaded" : "Not Loaded";
            
            _logger.LogDebug("Plugin health metrics generated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate complete health metrics: {Message}", ex.Message);
            metrics["HealthCheckError"] = ex.Message;
        }

        return metrics;
    }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = "Universal Timeline Manager",
                EmbeddedResourcePath = "Jellyfin.Plugin.TimelineManager.Configuration.configPage.html"
            }
        };
    }
}
