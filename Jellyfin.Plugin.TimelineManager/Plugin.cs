using System;
using System.Collections.Generic;
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
public class Plugin : BasePlugin<PluginConfiguration>
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
}
