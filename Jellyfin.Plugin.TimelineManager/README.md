# Universal Timeline Manager

**Watch your favorite movie universes in the right order!**

This Jellyfin plugin helps you organize movies and TV shows from cinematic universes (like Marvel, DC, Star Wars) in chronological order.

## What It Does

Creates organized playlists showing the correct viewing order for:
- Marvel Cinematic Universe
- Star Wars Saga
- DC Extended Universe
- Any other cinematic universe you want!

## Installation

1. Open Jellyfin → **Dashboard** → **Plugins** → **Repositories**
2. Click **Add** and enter:
   - **Name:** `Universal Timeline Manager`
   - **URL:** `https://raw.githubusercontent.com/ngdtam/jellyfin-timeline-plugin/main/manifest.json`
3. Go to **Catalog** → Find **Universal Timeline Manager** → Click **Install**
4. Restart Jellyfin

## How to Use

### Step 1: Create Configuration File

Create a file at `/config/timeline_manager_config.json` with this format:

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

### Step 2: Find Movie IDs

Get TMDB IDs from [themoviedb.org](https://www.themoviedb.org):
1. Search for your movie/show
2. Look at the URL: `https://www.themoviedb.org/movie/1771`
3. The number (`1771`) is your ID!

### Step 3: Done!

The plugin reads your configuration automatically.

**Need more help?** See [CONFIGURATION.md](../CONFIGURATION.md) for detailed instructions and examples.

## Examples

Ready-to-use configurations are in the `configurations/` folder:
- [Marvel Cinematic Universe](../configurations/mcu-complete.json)
- [DC Extended Universe](../configurations/dceu.json)
- [Star Wars](../configurations/star-wars-complete.json)

## Troubleshooting

**Plugin not showing?**
- Restart Jellyfin
- Check the plugin folder is in the right place

**Configuration not working?**
- Validate your JSON at [jsonlint.com](https://jsonlint.com)
- Check the file is at `/config/timeline_manager_config.json`

**Movies not found?**
- Make sure the TMDB ID is correct
- Check the movie is in your Jellyfin library

## Need Help?

- **Full guide:** [CONFIGURATION.md](../CONFIGURATION.md)
- **Report issues:** [GitHub Issues](https://github.com/ngdtam/jellyfin-timeline-plugin/issues)

## License

MIT License — Free to use!
