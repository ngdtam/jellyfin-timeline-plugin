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

- **Multiple Universe Support** — Configure unlimited cinematic universes in a single JSON file
- **Mixed Content Types** — Movies and TV episodes together in the same chronological playlist
- **Provider_ID Matching** — Uses TMDB and IMDB identifiers for 100% accurate content matching
- **Chronological Ordering** — Maintains perfect timeline order as specified in your configuration
- **Error Resilience** — Gracefully handles missing items and service failures without stopping
- **Performance Optimized** — O(1) lookup performance using dictionary structures for large libraries
- **Comprehensive Logging** — Detailed logging with troubleshooting guidance and progress tracking
- **Idempotent Operations** — Safe to run multiple times, updates existing playlists without duplicates

## Requirements

- Jellyfin Server **10.11.6** or higher
- **.NET 9.0** runtime
- Read access to configuration directory
- Playlist creation permissions

## Installation

### From Plugin Repository (Recommended)

1. **Dashboard → Plugins → Repositories → Add**
2. Enter:
   - Name: `Universal Timeline Manager`
   - URL: `https://raw.githubusercontent.com/your-username/jellyfin-plugin-timeline-manager/main/manifest.json`
3. **Catalog → Universal Timeline Manager → Install**
4. Restart Jellyfin

### Manual Installation

Download from [Releases](https://github.com/your-username/jellyfin-plugin-timeline-manager/releases) and extract to:

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

### 2. Run Timeline Task

1. Go to **Dashboard → Scheduled Tasks**
2. Find **"Universal Timeline Manager"**
3. Click **Run Now** or set up a schedule
4. Monitor progress in Jellyfin logs

### 3. View Your Playlists

Playlists appear in your Jellyfin library:
- **Marvel Cinematic Universe** — All MCU content in chronological order
- **Star Wars Saga** — All Star Wars content in timeline order

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
- Verify Provider_IDs match your library metadata
- Check that content exists in your Jellyfin library
- Review logs for specific missing item warnings

**Playlists not created?**
- Ensure user has playlist creation permissions
- Check Jellyfin logs for detailed error messages
- Verify Jellyfin services are running properly

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

The plugin includes comprehensive performance monitoring:
- Processing time per universe
- Item lookup performance metrics
- Memory usage tracking
- Error rate statistics

## How It Works

### Content Discovery
1. **Library Indexing** — Scans your Jellyfin library and builds O(1) lookup dictionaries
2. **Provider Matching** — Matches configuration Provider_IDs to library content
3. **Mixed Content Support** — Handles movies and TV episodes uniformly

### Playlist Management
1. **Chronological Ordering** — Maintains exact order from configuration
2. **Idempotent Updates** — Safely updates existing playlists without duplicates
3. **Error Resilience** — Continues processing other universes if one fails

### Automatic Synchronization
- **Scheduled Task** — Run manually or on a schedule
- **Progress Reporting** — Real-time progress updates in Jellyfin
- **Comprehensive Logging** — Detailed logs for troubleshooting

## Contributing

Interested in contributing? We welcome:
- **Bug reports** and feature requests
- **Configuration examples** for popular universes
- **Code contributions** and improvements
- **Documentation** updates and translations

See [CONTRIBUTING.md](CONTRIBUTING.md) for development setup and guidelines.

## Community Configurations

Check out community-contributed configurations:
- [Marvel Cinematic Universe (Complete)](configurations/mcu-complete.json)
- [DC Extended Universe](configurations/dceu.json)
- [Star Wars (All Media)](configurations/star-wars-complete.json)
- [Harry Potter Universe](configurations/harry-potter.json)

## License

[MIT License](LICENSE) - Feel free to use, modify, and distribute.