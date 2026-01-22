using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Plugin.TimelineManager.Models;
using Jellyfin.Plugin.TimelineManager.Services;
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
    private readonly string _configPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimelineConfigController"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public TimelineConfigController(ILogger<TimelineConfigController> logger)
    {
        _logger = logger;
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
            if (!System.IO.File.Exists(_configPath))
            {
                _logger.LogInformation("Configuration file not found, returning empty config");
                return Ok(new { universes = Array.Empty<object>() });
            }

            var jsonContent = await System.IO.File.ReadAllTextAsync(_configPath);
            var config = JsonSerializer.Deserialize<object>(jsonContent);
            
            return Ok(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading configuration");
            return Ok(new { universes = Array.Empty<object>() });
        }
    }

    /// <summary>
    /// Validates the provided timeline configuration.
    /// </summary>
    /// <param name="request">The configuration validation request.</param>
    /// <returns>Validation result.</returns>
    [HttpPost("Validate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<ValidationResponse> ValidateConfiguration([FromBody] ConfigRequest request)
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

            // Validate configuration
            var validationResult = configService.ValidateConfiguration(config);
            
            return Ok(new ValidationResponse
            {
                IsValid = validationResult.IsValid,
                Errors = validationResult.Errors.ToArray(),
                Message = validationResult.IsValid 
                    ? "Configuration is valid!" 
                    : "Configuration has validation errors"
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
