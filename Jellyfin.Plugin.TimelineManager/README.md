# Universal Timeline Manager

**Chronological playlists for cinematic universes in Jellyfin.**

Universal Timeline Manager automatically creates and maintains chronological playlists for multiple cinematic universes (Marvel, DC, Star Wars, etc.) based on JSON configuration files. Your media stays exactly where it is, but gets organized into perfect viewing order playlists.

```
/config/timeline_manager_config.json    ← Configuration file
{
  "universes": [
    {
      "key": "mcu",
      "name": "Marvel Cinematic Universe",
      "items": [
        {"providerId": "1771", "providerName": "tmdb", "type": "movie"},
        {"providerId": "tt0371746", "providerName": "imdb", "type": "movie"}
      ]
    }
  ]
}

Jellyfin Playlists:                      ← Automatically created playlists
├── Marvel Cinematic Universe
├── DC Extended Universe  
└── Star Wars Saga
```

## Features

- **REST API** — Validate and manage configurations via HTTP endpoints (v1.3.0)
- **Library Validation** — API checks if configured items exist in your Jellyfin library
- **Multiple Universe Support** — Configure unlimited cinematic universes in a single JSON file
- **Mixed Content Types** — Movies and TV episodes together in the same chronological playlist
- **Provider_ID Matching** — Uses TMDB and IMDB identifiers for 100% accurate content matching
- **Chronological Ordering** — Maintains perfect timeline order as specified in your configuration
- **Error Resilience** — Gracefully handles missing items and service failures without stopping
- **Performance Optimized** — O(1) lookup performance using dictionary structures for large libraries
- **Comprehensive Logging** — Detailed logging with troubleshooting guidance and progress tracking

## Configuration

**v1.3.0 is API-only.** Configuration is done via JSON file editing or REST API endpoints.

📖 **See [CONFIGURATION.md](../CONFIGURATION.md) for:**
- Complete configuration guide
- How to find Provider IDs
- API endpoint documentation
- Validation examples
- Docker instructions
- Troubleshooting tips

## Requirements

- Jellyfin Server **10.10.0** or higher
- **.NET 9.0** runtime
- Read/write access to configuration directory (`/config`)

## Installation

### From Plugin Repository (Recommended)

1. **Dashboard → Plugins → Repositories → Add**
2. Enter:
   - Name: `Universal Timeline Manager`
   - URL: `https://raw.githubusercontent.com/ngdtam/jellyfin-timeline-plugin/main/manifest.json`
3. **Catalog → Universal Timeline Manager → Install**
4. Restart Jellyfin

### Manual Installation

Download from [Releases](https://github.com/ngdtam/jellyfin-timeline-plugin/releases) and extract to:

- Linux: `/var/lib/jellyfin/plugins/Universal Timeline Manager/`
- Windows: `%ProgramData%\Jellyfin\Server\plugins\Universal Timeline Manager\`
- Docker: `/config/plugins/Universal Timeline Manager/`

## Quick Start

### 1. Create Configuration File

Create `/config/timeline_manager_config.json` in your Jellyfin config directory:

```json
{
  "universes": [
    {
      "key": "mcu",
      "name": "Marvel Cinematic Universe",
      "items": [
        {"providerId": "1771", "providerName": "tmdb", "type": "movie"},
        {"providerId": "1726", "providerName": "tmdb", "type": "movie"},
        {"providerId": "85271", "providerName": "tmdb", "type": "episode"}
      ]
    },
    {
      "key": "star-wars",
      "name": "Star Wars Saga",
      "items": [
        {"providerId": "1893", "providerName": "tmdb", "type": "movie"},
        {"providerId": "1894", "providerName": "tmdb", "type": "movie"}
      ]
    }
  ]
}
```

**📖 See [CONFIGURATION.md](../CONFIGURATION.md) for detailed configuration guide, examples, and API documentation.**

### 2. Validate Configuration (Optional)

Use the REST API to validate your configuration:

```bash
# Test API connection
curl http://localhost:8096/Timeline/Test

# Validate configuration (checks if items exist in your library)
curl -X POST http://localhost:8096/Timeline/Validate \
  -H "Content-Type: application/json" \
  -d '{"jsonContent": "{\"universes\":[...]}"}'
```

The validation endpoint will tell you exactly which items are found or missing in your Jellyfin library.

### 3. Access Your Configuration

The plugin is now installed and ready. You can:
- Edit the configuration file directly at `/config/timeline_manager_config.json`
- Use the REST API endpoints to validate and save configurations
- Monitor the plugin through Jellyfin logs

**Note:** v1.3.0 is API-only. There is no web UI configuration page.

## Configuration Reference

### Universe Structure

| Field | Type | Description | Example |
|-------|------|-------------|---------|
| `key` | string | Unique identifier (no spaces) | `"mcu"` |
| `name` | string | Display name for playlist | `"Marvel Cinematic Universe"` |
| `items` | array | Timeline items in chronological order | See below |

### Timeline Item Structure

| Field | Type | Description | Example |
|-------|------|-------------|---------|
| `providerId` | string | TMDB or IMDB identifier | `"1771"` or `"tt0371746"` |
| `providerName` | string | Provider type | `"tmdb"` or `"imdb"` |
| `type` | string | Content type | `"movie"` or `"episode"` |

### Supported Providers

- **TMDB (The Movie Database)**: Use numeric IDs (e.g., `"1771"`)
- **IMDB (Internet Movie Database)**: Use full IDs with "tt" prefix (e.g., `"tt0371746"`)

## Example Configurations

### Marvel Cinematic Universe (MCU)
```json
{
  "universes": [
    {
      "key": "mcu",
      "name": "Marvel Cinematic Universe",
      "items": [
        {"providerId": "1771", "providerName": "tmdb", "type": "movie"},
        {"providerId": "1726", "providerName": "tmdb", "type": "movie"},
        {"providerId": "10138", "providerName": "tmdb", "type": "movie"},
        {"providerId": "1724", "providerName": "tmdb", "type": "movie"}
      ]
    }
  ]
}
```

### DC Extended Universe
```json
{
  "universes": [
    {
      "key": "dceu",
      "name": "DC Extended Universe",
      "items": [
        {"providerId": "49026", "providerName": "tmdb", "type": "movie"},
        {"providerId": "297761", "providerName": "tmdb", "type": "movie"},
        {"providerId": "141052", "providerName": "tmdb", "type": "movie"}
      ]
    }
  ]
}
```

### Mixed Content (Movies + TV)
```json
{
  "universes": [
    {
      "key": "mcu-complete",
      "name": "MCU Complete Timeline",
      "items": [
        {"providerId": "1771", "providerName": "tmdb", "type": "movie"},
        {"providerId": "85271", "providerName": "tmdb", "type": "episode"},
        {"providerId": "1726", "providerName": "tmdb", "type": "movie"},
        {"providerId": "85271", "providerName": "tmdb", "type": "episode"}
      ]
    }
  ]
}
```

## Docker Configuration

The plugin works seamlessly with Docker. Just ensure your config directory is properly mounted:

```yaml
# docker-compose.yml
services:
  jellyfin:
    volumes:
      - /path/to/config:/config
      - /path/to/media:/media
    # Plugin will read from /config/timeline_manager_config.json
```

**Docker Users:** Edit configuration using:
```bash
# Method 1: Copy out, edit, copy back
docker cp jellyfin-container:/config/timeline_manager_config.json ./config.json
# Edit locally
docker cp ./config.json jellyfin-container:/config/timeline_manager_config.json

# Method 2: Edit directly in container
docker exec -it jellyfin-container vi /config/timeline_manager_config.json

# Method 3: Use volume mount (edit directly on host)
nano /path/to/jellyfin/config/timeline_manager_config.json
```

## REST API Reference

v1.3.0 provides REST API endpoints for configuration management:

### Test API Connection
```bash
GET http://localhost:8096/Timeline/Test
```
Returns API status and timestamp.

### Get Current Configuration
```bash
GET http://localhost:8096/Timeline/Config
```
Returns the current configuration JSON.

### Validate Configuration
```bash
POST http://localhost:8096/Timeline/Validate
Content-Type: application/json

{
  "jsonContent": "{\"universes\":[...]}"
}
```
Validates JSON structure and checks if items exist in your Jellyfin library.

### Save Configuration
```bash
POST http://localhost:8096/Timeline/Save
Content-Type: application/json

{
  "jsonContent": "{\"universes\":[...]}"
}
```
Validates and saves the configuration.

**📖 See [CONFIGURATION.md](../CONFIGURATION.md) for complete API documentation.**

## Troubleshooting

### Common Issues

**Plugin not appearing?**
- Ensure DLL is in correct plugins directory
- Restart Jellyfin server completely
- Check Jellyfin logs for plugin loading errors

**Configuration not loading?**
- Verify JSON syntax using a JSON validator
- Check file permissions on configuration file
- Ensure file is at `/config/timeline_manager_config.json`

**Items not found in playlists?**
- Use the `/Timeline/Validate` API endpoint to check which items are missing
- Verify Provider_IDs match your library metadata
- Check that content exists in your Jellyfin library
- Ensure metadata has been refreshed in Jellyfin

**Playlists not created?**
- Note: v1.3.0 focuses on configuration management via API
- Playlist creation feature is planned for future releases
- Currently, the plugin validates and manages configuration only

### Log Analysis

Enable debug logging in Jellyfin for detailed troubleshooting:

1. **Dashboard → Logs → Log Level → Debug**
2. Run the timeline task
3. Check logs for detailed processing information

**Log Locations:**
- Windows: `%ProgramData%\Jellyfin\Server\logs\`
- Linux: `/var/log/jellyfin/`
- Docker: Container logs or mounted log directory

### Performance Monitoring

The plugin includes performance monitoring for API operations:
- Configuration validation time
- Library lookup performance
- API response times

## How It Works

### Configuration Management (v1.3.0)
1. **JSON Configuration** — Edit `/config/timeline_manager_config.json` directly
2. **REST API** — Validate and save configurations via HTTP endpoints
3. **Library Validation** — API checks if configured Provider IDs exist in your library

### Content Discovery
1. **Library Indexing** — Scans your Jellyfin library and builds O(1) lookup dictionaries
2. **Provider Matching** — Matches configuration Provider_IDs to library content
3. **Mixed Content Support** — Handles movies and TV episodes uniformly

### Future Features (Planned)
- **Automatic Playlist Creation** — Generate playlists from validated configurations
- **Scheduled Tasks** — Automatic synchronization on a schedule
- **Progress Reporting** — Real-time progress updates in Jellyfin

## Contributing

Interested in contributing? We welcome:
- **Bug reports** and feature requests via [GitHub Issues](https://github.com/ngdtam/jellyfin-timeline-plugin/issues)
- **Configuration examples** for popular universes
- **Code contributions** and improvements via Pull Requests
- **Documentation** updates and translations

## Community Configurations

Check out community-contributed configurations in the `configurations/` directory:
- [Marvel Cinematic Universe (Complete)](../configurations/mcu-complete.json)
- [DC Extended Universe](../configurations/dceu.json)
- [Star Wars (All Media)](../configurations/star-wars-complete.json)

Want to contribute your own? Submit a PR with your configuration!

## License

[MIT License](LICENSE) - Feel free to use, modify, and distribute.