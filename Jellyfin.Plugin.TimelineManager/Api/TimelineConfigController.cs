using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Plugin.TimelineManager.Models;
using Jellyfin.Plugin.TimelineManager.Services;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TimelineManager.Api;

/// <summary>
/// API controller for timeline configuration management.
/// </summary>
[ApiController]
[Route("Timeline")]
[Authorize(Policy = "RequiresElevation")]
public class TimelineConfigController : ControllerBase
{
    private readonly ILogger<TimelineConfigController> _logger;
    private readonly ILibraryManager _libraryManager;
    private readonly string _configPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimelineConfigController"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="libraryManager">Jellyfin library manager.</param>
    public TimelineConfigController(ILogger<TimelineConfigController> logger, ILibraryManager libraryManager)
    {
        _logger = logger;
        _libraryManager = libraryManager;
        _configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "jellyfin",
            "config",
            "timeline_manager_config.json"
        );
    }

    /// <summary>
    /// Gets the current timeline configuration.
    /// </summary>
    /// <returns>The configuration JSON or empty object if not found.</returns>
    [HttpGet("Config")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> GetConfiguration()
    {
        try
        {
            _logger.LogInformation("[Timeline API] GetConfiguration called");
            
            if (!System.IO.File.Exists(_configPath))
            {
                _logger.LogInformation("Configuration file not found, returning empty config");
                return Ok(new { universes = Array.Empty<object>() });
            }

            var jsonContent = await System.IO.File.ReadAllTextAsync(_configPath);
            var config = JsonSerializer.Deserialize<object>(jsonContent);
            
            _logger.LogInformation("[Timeline API] Returning configuration");
            return Ok(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading configuration");
            return Ok(new { universes = Array.Empty<object>() });
        }
    }

    /// <summary>
    /// Diagnostic endpoint to test if API is working.
    /// </summary>
    [HttpGet("Test")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<object> TestEndpoint()
    {
        _logger.LogInformation("[Timeline API] Test endpoint called - API IS WORKING!");
        return Ok(new { 
            status = "API is working!", 
            timestamp = DateTime.UtcNow,
            message = "If you see this, the Timeline API controller is loaded and responding"
        });
    }

    /// <summary>
    /// Validates the provided timeline configuration.
    /// </summary>
    /// <param name="request">The configuration validation request.</param>
    /// <returns>Validation result.</returns>
    [HttpPost("Validate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<ValidationResponse>> ValidateConfiguration([FromBody] ConfigRequest request)
    {
        try
        {
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var configLogger = loggerFactory.CreateLogger<ConfigurationService>();
            var configService = new ConfigurationService(configLogger, _configPath);
            
            // Parse JSON
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                PropertyNameCaseInsensitive = true
            };

            var config = JsonSerializer.Deserialize<TimelineConfiguration>(request.JsonContent, options);
            
            if (config == null)
            {
                return Ok(new ValidationResponse
                {
                    IsValid = false,
                    Errors = new[] { "Failed to parse JSON configuration" }
                });
            }

            // Validate configuration structure
            var validationResult = configService.ValidateConfiguration(config);
            
            if (!validationResult.IsValid)
            {
                return Ok(new ValidationResponse
                {
                    IsValid = false,
                    Errors = validationResult.Errors.ToArray(),
                    Message = "Configuration has validation errors"
                });
            }

            // Now validate that content actually exists in Jellyfin
            var lookupLogger = loggerFactory.CreateLogger<ContentLookupService>();
            var lookupService = new ContentLookupService(lookupLogger, _libraryManager);
            
            _logger.LogInformation("Building lookup tables to validate content exists in library");
            lookupService.BuildLookupTables();
            
            var contentErrors = new List<string>();
            var foundItems = new List<string>();
            var foundCount = 0;
            var totalCount = 0;

            // Create HTTP client for TMDB lookups
            using var httpClient = new System.Net.Http.HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10); // 10 second timeout per TMDB request
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");

            // Collect all items first
            var allItems = new List<(Universe universe, TimelineItem item)>();
            foreach (var universe in config.Universes)
            {
                foreach (var item in universe.Items)
                {
                    allItems.Add((universe, item));
                    totalCount++;
                }
            }

            // Limit concurrent TMDB requests to avoid rate limiting
            var semaphore = new System.Threading.SemaphoreSlim(5, 5); // Max 5 concurrent requests

            // Process items in parallel for better performance
            var tasks = allItems.Select(async tuple =>
            {
                var (universe, item) = tuple;
                var itemId = lookupService.FindItemByProviderId(item.ProviderId, item.ProviderName, item.Type);
                
                if (itemId.HasValue)
                {
                    // Get the actual item to retrieve its name
                    var jellyfinItem = _libraryManager.GetItemById(itemId.Value);
                    var itemName = jellyfinItem?.Name ?? "Unknown";
                    return (found: true, message: $"[FOUND] {universe.Name}: {itemName} ({item.Type}) - {item.ProviderName}:{item.ProviderId}");
                }
                else
                {
                    // Try to fetch the name from TMDB with rate limiting
                    await semaphore.WaitAsync();
                    try
                    {
                        var itemName = await FetchTmdbTitle(httpClient, item.ProviderId, item.ProviderName, item.Type);
                        return (found: false, message: $"[MISSING] {universe.Name}: {itemName} ({item.Type}) - {item.ProviderName}:{item.ProviderId}");
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }
            });

            var results = await Task.WhenAll(tasks);

            foreach (var result in results)
            {
                if (result.found)
                {
                    foundCount++;
                    foundItems.Add(result.message);
                }
                else
                {
                    contentErrors.Add(result.message);
                }
            }

            if (contentErrors.Count > 0)
            {
                return Ok(new ValidationResponse
                {
                    IsValid = false,
                    Errors = contentErrors.ToArray(),
                    FoundItems = foundItems.ToArray(),
                    Message = $"Found {foundCount}/{totalCount} items in your library. {contentErrors.Count} items are missing."
                });
            }
            
            return Ok(new ValidationResponse
            {
                IsValid = true,
                Message = $"Configuration is valid! All {totalCount} items found in your Jellyfin library.",
                FoundItems = foundItems.ToArray(),
                Errors = Array.Empty<string>()
            });
        }
        catch (JsonException ex)
        {
            return Ok(new ValidationResponse
            {
                IsValid = false,
                Errors = new[] { $"JSON parsing error: {ex.Message}" }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating configuration");
            return Ok(new ValidationResponse
            {
                IsValid = false,
                Errors = new[] { $"Validation error: {ex.Message}" }
            });
        }
    }

    /// <summary>
    /// Saves the timeline configuration to file.
    /// </summary>
    /// <param name="request">The configuration save request.</param>
    /// <returns>Save result.</returns>
    [HttpPost("Save")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<SaveResponse>> SaveConfiguration([FromBody] ConfigRequest request)
    {
        try
        {
            // First validate
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var configLogger = loggerFactory.CreateLogger<ConfigurationService>();
            var configService = new ConfigurationService(configLogger, _configPath);
            
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                PropertyNameCaseInsensitive = true
            };

            var config = JsonSerializer.Deserialize<TimelineConfiguration>(request.JsonContent, options);
            
            if (config == null)
            {
                return Ok(new SaveResponse
                {
                    Success = false,
                    Message = "Failed to parse JSON configuration"
                });
            }

            var validationResult = configService.ValidateConfiguration(config);
            
            if (!validationResult.IsValid)
            {
                return Ok(new SaveResponse
                {
                    Success = false,
                    Message = "Configuration validation failed",
                    Errors = validationResult.Errors.ToArray()
                });
            }

            // Ensure directory exists
            var directory = Path.GetDirectoryName(_configPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Pretty print JSON for readability
            var prettyJson = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            // Save to file
            await System.IO.File.WriteAllTextAsync(_configPath, prettyJson);
            
            _logger.LogInformation("Configuration saved successfully to {ConfigPath}", _configPath);
            
            return Ok(new SaveResponse
            {
                Success = true,
                Message = "Configuration saved successfully!"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving configuration");
            return Ok(new SaveResponse
            {
                Success = false,
                Message = $"Error saving configuration: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Fetches the title of a movie or TV series from TMDB website.
    /// </summary>
    /// <param name="httpClient">HTTP client for making requests.</param>
    /// <param name="providerId">The TMDB ID.</param>
    /// <param name="providerName">The provider name (tmdb or imdb).</param>
    /// <param name="contentType">The content type (movie or episode).</param>
    /// <returns>The title of the item, or a fallback string if not found.</returns>
    private async Task<string> FetchTmdbTitle(System.Net.Http.HttpClient httpClient, string providerId, string providerName, string contentType)
    {
        // Only support TMDB lookups for now
        if (providerName.ToLowerInvariant() != "tmdb")
        {
            return $"Unknown {contentType} (ID:{providerId})";
        }

        try
        {
            string url;
            string titlePattern;
            
            if (contentType.ToLowerInvariant() == "movie")
            {
                url = $"https://www.themoviedb.org/movie/{providerId}";
                titlePattern = @"<title>(.+?)\s*\(\d{4}\)";
            }
            else if (contentType.ToLowerInvariant() == "episode")
            {
                // For episodes, the providerId is actually the series ID
                // Fetch the series name instead
                url = $"https://www.themoviedb.org/tv/{providerId}";
                titlePattern = @"<title>(.+?)\s*\(TV Series";
            }
            else
            {
                return $"Unknown {contentType} (TMDB:{providerId})";
            }

            var html = await httpClient.GetStringAsync(url);
            
            // Extract title from HTML <title> tag
            var titleMatch = System.Text.RegularExpressions.Regex.Match(html, titlePattern);
            if (titleMatch.Success && titleMatch.Groups.Count > 1)
            {
                var title = titleMatch.Groups[1].Value.Trim();
                // Remove any HTML entities
                title = System.Net.WebUtility.HtmlDecode(title);
                
                // Add "(series)" suffix for TV shows to distinguish from movies
                if (contentType.ToLowerInvariant() == "episode")
                {
                    title = $"{title} (series)";
                }
                
                return title;
            }
            
            return $"Unknown {contentType} (TMDB:{providerId})";
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to fetch TMDB title for {ProviderId}: {Message}", providerId, ex.Message);
            return $"Unknown {contentType} (TMDB:{providerId})";
        }
    }
}

/// <summary>
/// Request model for configuration operations.
/// </summary>
public class ConfigRequest
{
    /// <summary>
    /// Gets or sets the JSON content.
    /// </summary>
    public string JsonContent { get; set; } = string.Empty;
}

/// <summary>
/// Response model for validation operations.
/// </summary>
public class ValidationResponse
{
    /// <summary>
    /// Gets or sets a value indicating whether the configuration is valid.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets the validation message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the validation errors.
    /// </summary>
    public string[] Errors { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets the items that were found in the library.
    /// </summary>
    public string[] FoundItems { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Response model for save operations.
/// </summary>
public class SaveResponse
{
    /// <summary>
    /// Gets or sets a value indicating whether the save was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the response message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets any errors that occurred.
    /// </summary>
    public string[] Errors { get; set; } = Array.Empty<string>();
}
