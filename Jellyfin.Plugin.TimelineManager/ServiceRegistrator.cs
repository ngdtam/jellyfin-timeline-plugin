using Jellyfin.Plugin.TimelineManager.Services;
using MediaBrowser.Common;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.TimelineManager;

/// <summary>
/// Service registrator for Timeline Manager plugin services.
/// </summary>
public sealed class ServiceRegistrator : IPluginServiceRegistrator
{
    /// <summary>
    /// Registers services for the Timeline Manager plugin.
    /// </summary>
    /// <param name="serviceCollection">The service collection.</param>
    /// <param name="applicationHost">The application host.</param>
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        // Register UniverseManagementService as singleton
        serviceCollection.AddSingleton<UniverseManagementService>();
        
        // Register MigrationService as singleton
        serviceCollection.AddSingleton<MigrationService>();
        
        // Register ConfigurationService as singleton
        serviceCollection.AddSingleton<ConfigurationService>();
        
        // Register ContentSearchService as singleton
        serviceCollection.AddSingleton<ContentSearchService>();
        
        // Register TmdbSearchService as singleton
        serviceCollection.AddSingleton<TmdbSearchService>();
        
        // Register HttpClientFactory if not already registered
        serviceCollection.AddHttpClient();
    }
}
