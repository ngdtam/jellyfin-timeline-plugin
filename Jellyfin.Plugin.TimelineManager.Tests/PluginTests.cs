using System;
using Moq;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;
using Jellyfin.Plugin.TimelineManager;
using Xunit;

namespace Jellyfin.Plugin.TimelineManager.Tests;

/// <summary>
/// Unit tests for the Plugin class.
/// </summary>
public class PluginTests
{
    /// <summary>
    /// Test that the plugin has a unique GUID identifier.
    /// </summary>
    [Fact]
    public void Plugin_Should_Have_Unique_Guid()
    {
        // Arrange & Act
        var expectedGuid = Guid.Parse("12345678-1234-5678-9abc-123456789012");
        var mockLogger = Mock.Of<ILogger<Plugin>>();
        
        // Assert
        Assert.Equal(expectedGuid, new Plugin(Mock.Of<IApplicationPaths>(), Mock.Of<IXmlSerializer>(), mockLogger).Id);
    }

    /// <summary>
    /// Test that the plugin has the correct name.
    /// </summary>
    [Fact]
    public void Plugin_Should_Have_Correct_Name()
    {
        // Arrange
        var mockApplicationPaths = Mock.Of<IApplicationPaths>();
        var mockXmlSerializer = Mock.Of<IXmlSerializer>();
        var mockLogger = Mock.Of<ILogger<Plugin>>();
        
        // Act
        var plugin = new Plugin(mockApplicationPaths, mockXmlSerializer, mockLogger);
        
        // Assert
        Assert.Equal("Universal Timeline Manager", plugin.Name);
    }

    /// <summary>
    /// Test that the plugin registers itself as a singleton instance.
    /// </summary>
    [Fact]
    public void Plugin_Should_Register_Singleton_Instance()
    {
        // Arrange
        var mockApplicationPaths = Mock.Of<IApplicationPaths>();
        var mockXmlSerializer = Mock.Of<IXmlSerializer>();
        var mockLogger = Mock.Of<ILogger<Plugin>>();
        
        // Act
        var plugin = new Plugin(mockApplicationPaths, mockXmlSerializer, mockLogger);
        
        // Assert
        Assert.NotNull(Plugin.Instance);
        Assert.Same(plugin, Plugin.Instance);
    }

    /// <summary>
    /// Test that the plugin implements IHasWebPages interface correctly.
    /// </summary>
    [Fact]
    public void Plugin_Should_Implement_IHasWebPages()
    {
        // Arrange
        var mockApplicationPaths = Mock.Of<IApplicationPaths>();
        var mockXmlSerializer = Mock.Of<IXmlSerializer>();
        var mockLogger = Mock.Of<ILogger<Plugin>>();
        
        // Act
        var plugin = new Plugin(mockApplicationPaths, mockXmlSerializer, mockLogger);
        var pages = plugin.GetPages();
        
        // Assert
        Assert.NotNull(pages);
        Assert.Empty(pages); // No web pages configured initially
    }

    /// <summary>
    /// Test that the plugin inherits from BasePlugin correctly.
    /// </summary>
    [Fact]
    public void Plugin_Should_Inherit_From_BasePlugin()
    {
        // Arrange
        var mockApplicationPaths = Mock.Of<IApplicationPaths>();
        var mockXmlSerializer = Mock.Of<IXmlSerializer>();
        var mockLogger = Mock.Of<ILogger<Plugin>>();
        
        // Act
        var plugin = new Plugin(mockApplicationPaths, mockXmlSerializer, mockLogger);
        
        // Assert
        Assert.IsAssignableFrom<MediaBrowser.Common.Plugins.BasePlugin<PluginConfiguration>>(plugin);
    }
}
