# Universal Timeline Manager

**Watch your favorite movie universes in the right order!**

This plugin helps you organize movies and TV shows from cinematic universes (like Marvel, DC, Star Wars) in chronological order. Perfect for binge-watching in timeline order instead of release order.

## What Does It Do?

Creates organized playlists like:
- **Marvel Cinematic Universe** — All MCU movies/shows in story order
- **Star Wars Saga** — Episodes 1-9 plus shows in timeline order
- **DC Extended Universe** — All DCEU content chronologically

Your files stay where they are. The plugin just creates playlists showing the correct viewing order.

## Requirements

- Jellyfin Server version **10.10.0** or newer
- That's it!

## Installation

### Easy Way (Recommended)

1. Open Jellyfin → **Dashboard** → **Plugins** → **Repositories**
2. Click **Add** and enter:
   - **Name:** `Universal Timeline Manager`
   - **URL:** `https://raw.githubusercontent.com/ngdtam/jellyfin-timeline-plugin/main/manifest.json`
3. Go to **Catalog** → Find **Universal Timeline Manager** → Click **Install**
4. Restart Jellyfin

### Manual Way

1. Download the latest release from [here](https://github.com/ngdtam/jellyfin-timeline-plugin/releases)
2. Extract the ZIP file
3. Copy the folder to your Jellyfin plugins directory:
   - **Windows:** `C:\ProgramData\Jellyfin\Server\plugins\Universal Timeline Manager\`
   - **Linux:** `/var/lib/jellyfin/plugins/Universal Timeline Manager/`
   - **Docker:** `/config/plugins/Universal Timeline Manager/`
4. Restart Jellyfin

## How to Use

### Step 1: Create Your Configuration File

You need to create a file that tells the plugin which movies/shows to include. This file goes in your Jellyfin config folder.

**File location:** `/config/timeline_manager_config.json`

**What it looks like:**
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

**What each part means:**
- `key` — A short name (no spaces)
- `name` — The playlist name you'll see in Jellyfin
- `providerId` — The movie/show ID from TMDB or IMDB
- `providerName` — Either `"tmdb"` or `"imdb"`
- `type` — Either `"movie"` or `"episode"`

### Step 2: Find Movie/Show IDs

You need the TMDB or IMDB ID for each movie/show:

**Option 1: From TMDB (easiest)**
1. Go to [themoviedb.org](https://www.themoviedb.org)
2. Search for your movie/show
3. Look at the URL: `https://www.themoviedb.org/movie/1771`
4. The number at the end (`1771`) is your ID

**Option 2: From Jellyfin**
1. Right-click the movie/show in Jellyfin
2. Click **Edit Metadata**
3. Go to **External IDs** tab
4. Copy the TMDB or IMDB ID

### Step 3: You're Done!

The plugin will read your configuration file automatically. No need to run anything or click buttons.

**Want more examples?** Check out [CONFIGURATION.md](CONFIGURATION.md) for ready-to-use configurations.

## Configuration Reference

**Quick Reference Table:**

| What | Type | Example | Notes |
|------|------|---------|-------|
| Universe key | text | `"mcu"` | Short name, no spaces |
| Universe name | text | `"Marvel Cinematic Universe"` | What you'll see in Jellyfin |
| Provider ID | number/text | `"1771"` or `"tt0371746"` | From TMDB or IMDB |
| Provider name | text | `"tmdb"` or `"imdb"` | Which website the ID is from |
| Type | text | `"movie"` or `"episode"` | What kind of content |

## Ready-to-Use Examples

Copy these examples and modify them for your library!

### Marvel Cinematic Universe
```json
{
  "universes": [
    {
      "key": "mcu",
      "name": "Marvel Cinematic Universe",
      "items": [
        {"providerId": "1771", "providerName": "tmdb", "type": "movie"},
        {"providerId": "1726", "providerName": "tmdb", "type": "movie"},
        {"providerId": "10138", "providerName": "tmdb", "type": "movie"}
      ]
    }
  ]
}
```

### Star Wars
```json
{
  "universes": [
    {
      "key": "star-wars",
      "name": "Star Wars Saga",
      "items": [
        {"providerId": "1893", "providerName": "tmdb", "type": "movie"},
        {"providerId": "1894", "providerName": "tmdb", "type": "movie"},
        {"providerId": "1895", "providerName": "tmdb", "type": "movie"}
      ]
    }
  ]
}
```

### Multiple Universes in One File
```json
{
  "universes": [
    {
      "key": "mcu",
      "name": "Marvel Cinematic Universe",
      "items": [
        {"providerId": "1771", "providerName": "tmdb", "type": "movie"}
      ]
    },
    {
      "key": "star-wars",
      "name": "Star Wars Saga",
      "items": [
        {"providerId": "1893", "providerName": "tmdb", "type": "movie"}
      ]
    }
  ]
}
```

**More examples:** Check the `configurations/` folder in this repository for complete MCU, DCEU, and Star Wars configurations!

## For Docker Users

If you're running Jellyfin in Docker, you can edit the configuration file in a few ways:

**Option 1: Edit on your computer (easiest)**
```bash
# If you have a volume mounted, just edit the file directly:
notepad C:\path\to\jellyfin\config\timeline_manager_config.json
```

**Option 2: Copy out, edit, copy back**
```bash
# Copy the file from Docker to your computer
docker cp jellyfin-container:/config/timeline_manager_config.json ./config.json

# Edit it with any text editor
# Then copy it back
docker cp ./config.json jellyfin-container:/config/timeline_manager_config.json
```

**Option 3: Edit inside Docker**
```bash
docker exec -it jellyfin-container vi /config/timeline_manager_config.json
```

## Troubleshooting

### Plugin not showing up?
1. Make sure you restarted Jellyfin after installing
2. Check that the plugin folder is in the right place
3. Look at Jellyfin logs for any error messages

### Configuration file not working?
1. Check your JSON syntax at [jsonlint.com](https://jsonlint.com) — one missing comma can break everything!
2. Make sure the file is at `/config/timeline_manager_config.json`
3. Check file permissions (Jellyfin needs to be able to read it)

### Movies/shows not appearing?
1. Make sure the TMDB/IMDB ID is correct
2. Check that the movie/show is actually in your Jellyfin library
3. Try refreshing metadata in Jellyfin

### Still having issues?
- Check [GitHub Issues](https://github.com/ngdtam/jellyfin-timeline-plugin/issues) to see if others had the same problem
- Create a new issue with your configuration file (remove any personal info first!)

## Need Help?

- **Full documentation:** [CONFIGURATION.md](CONFIGURATION.md)
- **Example configurations:** Check the `configurations/` folder
- **Report bugs:** [GitHub Issues](https://github.com/ngdtam/jellyfin-timeline-plugin/issues)
- **Contribute:** Pull requests welcome!

## License

MIT License — Free to use and modify!