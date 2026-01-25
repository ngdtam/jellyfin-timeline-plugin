# Universal Timeline Manager

**Watch your favorite movie universes in chronological order!**

A Jellyfin plugin that creates playlists for cinematic universes (Marvel, DC, Star Wars, etc.) in timeline order instead of release order.

## Features

- Create chronological playlists from any movie/TV universe
- Web UI for easy playlist management
- Support for both movies and TV show seasons
- Works with TMDB and IMDB IDs

## Installation

### Method 1: Plugin Repository (Recommended)

1. Open Jellyfin → **Dashboard** → **Plugins** → **Repositories**
2. Add repository:
   - **Name:** `Universal Timeline Manager`
   - **URL:** `https://raw.githubusercontent.com/ngdtam/jellyfin-timeline-plugin/main/manifest.json`
3. Go to **Catalog** → Install **Universal Timeline Manager**
4. Restart Jellyfin

### Method 2: Manual Installation

1. Download the latest release from [Releases](https://github.com/ngdtam/jellyfin-timeline-plugin/releases)
2. Extract to your Jellyfin plugins directory
3. Restart Jellyfin

## Quick Start

1. **Create configuration file** at `/config/timeline_manager_config.json`:

```json
{
  "universes": [
    {
      "key": "mcu",
      "name": "Marvel Cinematic Universe",
      "items": [
        {"providerId": "1771", "providerName": "tmdb", "type": "movie"},
        {"providerId": "1726", "providerName": "tmdb", "type": "movie"}
      ]
    }
  ]
}
```

2. **Open plugin page**: Dashboard → Plugins → Universal Timeline Manager

3. **Click "Create Playlists"** button

4. **View playlists**: Libraries → Playlists

## Finding Movie/Show IDs

**From TMDB:**
- Go to [themoviedb.org](https://www.themoviedb.org)
- Search for your movie/show
- Get ID from URL: `themoviedb.org/movie/1771` → ID is `1771`

**From Jellyfin:**
- Right-click movie/show → Edit Metadata → External IDs tab

## Configuration Examples

Check the `configurations/` folder for ready-to-use examples:
- Marvel Cinematic Universe
- Star Wars
- DC Extended Universe

## Documentation

- **[HOW_TO_USE.md](HOW_TO_USE.md)** - Complete usage guide
- **[CONFIGURATION.md](CONFIGURATION.md)** - Configuration file details
- **[DEPLOYMENT_GUIDE.md](DEPLOYMENT_GUIDE.md)** - Development and deployment

## Requirements

- Jellyfin Server 10.10.0 or newer

## Support

- **Issues:** [GitHub Issues](https://github.com/ngdtam/jellyfin-timeline-plugin/issues)
- **Examples:** Check `configurations/` folder

## License

MIT License
