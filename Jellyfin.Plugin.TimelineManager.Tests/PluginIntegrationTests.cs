using System;
using System.Linq;
using Jellyfin.Plugin.TimelineManager.Services;
using Jellyfin.Plugin.TimelineManager.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Playlists;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.TimelineManager.Tests;

/// <summary>
/// Integration tests for the main Plugin class and service registration.
/// </summary>
public class PluginIntegrationTests
{
    /// <summary>
    /// Test that the plugin initializes correctly with all required dependencies.
    /// </summary>
    [Fact]
    public void Plugin_InitializesCorrectly()
    {
        // Arrange
        var mockApplicationPaths = new Mock<IApplicationPaths>();
        var mockXmlSerializer = new Mock<IXmlSerializer>();
        var mockLogger = new Mock<ILogger<Plugin>>();

        // Act
        var plugin = new Plugin(mockApplicationPaths.Object, mockXmlSerializer.Object, mockLogger.Object);

        // Assert
        Assert.NotNull(plugin);
        Assert.Equal("Universal Timeline Manager", plugin.Name);
        Assert.Equal(Guid.Parse("12345678-1234-5678-9abc-123456789012"), plugin.Id);
        Assert.NotNull(plugin.Description);
        Assert.Equal(plugin, Plugin.Instance);
    }

    /// <summary>
    /// Test that service registration works correctly with all required services.
    /// </summary>
    [Fact]
    public void Plugin_RegistersAllServicesCorrectly()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();
        
        // Add required Jellyfin services as mocks
        serviceCollection.AddSingleton(Mock.Of<ILibraryManager>());
        serviceCollection.AddSingleton(Mock.Of<IPlaylistManager>());
        serviceCollection.AddSingleton(Mock.Of<ILogger<ConfigurationService>>());
        serviceCollection.AddSingleton(Mock.Of<ILogger<ContentLookupService>>());
        serviceCollection.AddSingleton(Mock.Of<ILogger<ProviderMatchingService>>());
        serviceCollection.AddSingleton(Mock.Of<ILogger<MixedContentService>>());
        serviceCollection.AddSingleton(Mock.Of<ILogger<PlaylistService>>());
        serviceCollection.AddSingleton(Mock.Of<ILogger<MixedContentPlaylistService>>());
        serviceCollection.AddSingleton(Mock.Of<ILogger<PlaylistErrorHandler>>());

        var mockApplicationPaths = new Mock<IApplicationPaths>();
        var mockXmlSerializer = new Mock<IXmlSerializer>();
        var mockLogger = new Mock<ILogger<Plugin>>();

        var plugin = new Plugin(mockApplicationPaths.Object, mockXmlSerializer.Object, mockLogger.Object);

        // Act
        plugin.RegisterServices(serviceCollection);
        var serviceProvider = serviceCollection.BuildServiceProvider();

        // Assert - Verify all services are registered
        Assert.NotNull(serviceProvider.GetService<IScheduledTask>());
        Assert.IsType<TimelineConfigTask>(serviceProvider.GetService<IScheduledTask>());
        
        Assert.NotNull(serviceProvider.GetService<ConfigurationService>());
        Assert.NotNull(serviceProvider.GetService<ContentLookupService>());
        Assert.NotNull(serviceProvider.GetService<ProviderMatchingService>());
        Assert.NotNull(serviceProvider.GetService<MixedContentService>());
        Assert.NotNull(serviceProvider.GetService<PlaylistService>());
        Assert.NotNull(serviceProvider.GetService<MixedContentPlaylistService>());
        Assert.NotNull(serviceProvider.GetService<PlaylistErrorHandler>());
    }

    /// <summary>
    /// Test that service validation works correctly.
    /// </summary>
    [Fact]
    public void Plugin_ValidatesServiceRegistrationCorrectly()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();
        
        // Add all required services
        serviceCollection.AddSingleton(Mock.Of<ILibraryManager>());
        serviceCollection.AddSingleton(Mock.Of<IPlaylistManager>());
        serviceCollection.AddSingleton(Mock.Of<ILogger<ConfigurationService>>());
        serviceCollection.AddSingleton(Mock.Of<ILogger<ContentLookupService>>());
        serviceCollection.AddSingleton(Mock.Of<ILogger<ProviderMatchingService>>());
        serviceCollection.AddSingleton(Mock.Of<ILogger<MixedContentService>>());
        serviceCollection.AddSingleton(Mock.Of<ILogger<PlaylistService>>());
        serviceCollection.AddSingleton(Mock.Of<ILogger<MixedContentPlaylistService>>());
        serviceCollection.AddSingleton(Mock.Of<ILogger<PlaylistErrorHandler>>());

        var mockApplicationPaths = new Mock<IApplicationPaths>();
        var mockXmlSerializer = new Mock<IXmlSerializer>();
        var mockLogger = new Mock<ILogger<Plugin>>();

        var plugin = new Plugin(mockApplicationPaths.Object, mockXmlSerializer.Object, mockLogger.Object);
        plugin.RegisterServices(serviceCollection);
        var serviceProvider = serviceCollection.BuildServiceProvider();

        // Act
        var isValid = plugin.ValidateServiceRegistration(serviceProvider);

        // Assert
        Assert.True(isValid);
    }

    /// <summary>
    /// Test that service validation fails when required services are missing.
    /// </summary>
    [Fact]
    public void Plugin_ValidatesServiceRegistrationFailsWhenServicesMissing()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();
        // Intentionally not adding all required services
        
        var mockApplicationPaths = new Mock<IApplicationPaths>();
        var mockXmlSerializer = new Mock<IXmlSerializer>();
        var mockLogger = new Mock<ILogger<Plugin>>();

        var plugin = new Plugin(mockApplicationPaths.Object, mockXmlSerializer.Object, mockLogger.Object);
        var serviceProvider = serviceCollection.BuildServiceProvider();

        // Act
        var isValid = plugin.ValidateServiceRegistration(serviceProvider);

        // Assert
        Assert.False(isValid);
    }

    /// <summary>
    /// Test that health metrics are generated correctly.
    /// </summary>
    [Fact]
    public void Plugin_GeneratesHealthMetricsCorrectly()
    {
        // Arrange
        var mockApplicationPaths = new Mock<IApplicationPaths>();
        var mockXmlSerializer = new Mock<IXmlSerializer>();
        var mockLogger = new Mock<ILogger<Plugin>>();

        var plugin = new Plugin(mockApplicationPaths.Object, mockXmlSerializer.Object, mockLogger.Object);

        // Act
        var metrics = plugin.GetHealthMetrics();

        // Assert
        Assert.NotNull(metrics);
        Assert.True(metrics.ContainsKey("PluginName"));
        Assert.True(metrics.ContainsKey("PluginId"));
        Assert.True(metrics.ContainsKey("IsActive"));
        Assert.True(metrics.ContainsKey("ConfigurationStatus"));
        
        Assert.Equal("Universal Timeline Manager", metrics["PluginName"]);
        Assert.Equal("12345678-1234-5678-9abc-123456789012", metrics["PluginId"]);
        Assert.Equal(true, metrics["IsActive"]);
    }

    /// <summary>
    /// Test that GetPages returns empty collection as expected.
    /// </summary>
    [Fact]
    public void Plugin_GetPagesReturnsEmptyCollection()
    {
        // Arrange
        var mockApplicationPaths = new Mock<IApplicationPaths>();
        var mockXmlSerializer = new Mock<IXmlSerializer>();
        var mockLogger = new Mock<ILogger<Plugin>>();

        var plugin = new Plugin(mockApplicationPaths.Object, mockXmlSerializer.Object, mockLogger.Object);

        // Act
        var pages = plugin.GetPages();

        // Assert
        Assert.NotNull(pages);
        Assert.Empty(pages);
    }

    /// <summary>
    /// Test that service registration handles exceptions gracefully.
    /// </summary>
    [Fact]
    public void Plugin_ServiceRegistrationHandlesExceptionsGracefully()
    {
        // Arrange
        var mockServiceCollection = new Mock<IServiceCollection>();
        mockServiceCollection.Setup(x => x.Add(It.IsAny<ServiceDescriptor>()))
                            .Throws(new InvalidOperationException("Service registration failed"));

        var mockApplicationPaths = new Mock<IApplicationPaths>();
        var mockXmlSerializer = new Mock<IXmlSerializer>();
        var mockLogger = new Mock<ILogger<Plugin>>();

        var plugin = new Plugin(mockApplicationPaths.Object, mockXmlSerializer.Object, mockLogger.Object);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => plugin.RegisterServices(mockServiceCollection.Object));
    }

    /// <summary>
    /// Test that service dependencies are correctly wired.
    /// </summary>
    [Fact]
    public void Plugin_ServiceDependenciesAreCorrectlyWired()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();
        
        // Add required Jellyfin services
        var mockLibraryManager = new Mock<ILibraryManager>();
        var mockPlaylistManager = new Mock<IPlaylistManager>();
        
        serviceCollection.AddSingleton(mockLibraryManager.Object);
        serviceCollection.AddSingleton(mockPlaylistManager.Object);
        serviceCollection.AddSingleton(Mock.Of<ILogger<ConfigurationService>>());
        serviceCollection.AddSingleton(Mock.Of<ILogger<ContentLookupService>>());
        serviceCollection.AddSingleton(Mock.Of<ILogger<ProviderMatchingService>>());
        serviceCollection.AddSingleton(Mock.Of<ILogger<MixedContentService>>());
        serviceCollection.AddSingleton(Mock.Of<ILogger<PlaylistService>>());
        serviceCollection.AddSingleton(Mock.Of<ILogger<MixedContentPlaylistService>>());
        serviceCollection.AddSingleton(Mock.Of<ILogger<PlaylistErrorHandler>>());

        var mockApplicationPaths = new Mock<IApplicationPaths>();
        var mockXmlSerializer = new Mock<IXmlSerializer>();
        var mockLogger = new Mock<ILogger<Plugin>>();

        var plugin = new Plugin(mockApplicationPaths.Object, mockXmlSerializer.Object, mockLogger.Object);

        // Act
        plugin.RegisterServices(serviceCollection);
        var serviceProvider = serviceCollection.BuildServiceProvider();

        // Assert - Verify service dependencies are correctly resolved
        var contentLookupService = serviceProvider.GetService<ContentLookupService>();
        Assert.NotNull(contentLookupService);

        var providerMatchingService = serviceProvider.GetService<ProviderMatchingService>();
        Assert.NotNull(providerMatchingService);

        var mixedContentService = serviceProvider.GetService<MixedContentService>();
        Assert.NotNull(mixedContentService);

        var playlistService = serviceProvider.GetService<PlaylistService>();
        Assert.NotNull(playlistService);

        var mixedContentPlaylistService = serviceProvider.GetService<MixedContentPlaylistService>();
        Assert.NotNull(mixedContentPlaylistService);

        var timelineTask = serviceProvider.GetService<IScheduledTask>() as TimelineConfigTask;
        Assert.NotNull(timelineTask);
    }
}