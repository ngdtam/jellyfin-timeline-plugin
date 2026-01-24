# Configuration Guide

## What You Need to Know

This plugin reads a simple text file (JSON format) that lists which movies and TV shows to include in your chronological playlists. Don't worry if you've never edited JSON before — it's just text with some specific formatting rules.

## Where is the Configuration File?

The file must be at: `/config/timeline_manager_config.json`

**Where is `/config`?**
- **Windows:** `C:\ProgramData\Jellyfin\Server\config\`
- **Linux:** `/var/lib/jellyfin/config/`
- **Docker:** Inside the container at `/config/` (or wherever you mounted it)

## Basic Structure

Here's the simplest possible configuration:

```json
{
  "universes": [
    {
      "key": "mcu",
      "name": "Marvel Cinematic Universe",
      "items": [
        {
          "providerId": "1771",
          "providerName": "tmdb",
          "type": "movie"
        },
        {
          "providerId": "1399",
          "providerName": "tmdb",
          "type": "episode"
        }
      ]
    }
  ]
}
```

**What each part means:**

- `universes` — The list of all your playlists (you can have multiple)
- `key` — A short name for the playlist (no spaces, lowercase)
- `name` — The actual playlist name you'll see in Jellyfin
- `items` — The list of movies/shows in this playlist
- `providerId` — The ID number from TMDB or IMDB
- `providerName` — Either `"tmdb"` or `"imdb"`
- `type` — Either `"movie"` or `"episode"`
- `season` — (Optional) Season number for TV shows (e.g., `1`, `2`, `3`)

## How to Find IDs

**Method 1: From TMDB (Easiest)**
1. Go to [themoviedb.org](https://www.themoviedb.org)
2. Search for your movie or TV show
3. Look at the URL: `https://www.themoviedb.org/movie/1771`
4. The number at the end (`1771`) is your ID!

**Method 2: From Jellyfin**
1. Right-click the movie/show in Jellyfin
2. Click "Edit Metadata"
3. Go to "External IDs" tab
4. Copy the TMDB or IMDB ID

**Method 3: From IMDB**
1. Go to [imdb.com](https://www.imdb.com)
2. Search for your content
3. Look at the URL: `https://www.imdb.com/title/tt0371746/`
4. The part with "tt" (`tt0371746`) is your ID!

## Complete Examples

Copy and paste these, then modify for your library!

### Marvel Cinematic Universe

```json
{
  "universes": [
    {
      "key": "mcu",
      "name": "Marvel Cinematic Universe",
      "items": [
        {"providerId": "1771", "providerName": "tmdb", "type": "movie"},
        {"providerId": "10138", "providerName": "tmdb", "type": "movie"},
        {"providerId": "84958", "providerName": "tmdb", "type": "episode", "season": 1},
        {"providerId": "84958", "providerName": "tmdb", "type": "episode", "season": 2}
      ]
    }
  ]
}
```

**Note:** The example above shows how to include specific seasons of a TV show. Loki Season 1 and Season 2 are listed separately with `"season": 1` and `"season": 2`.

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

**Want more?** Check the `configurations/` folder in the GitHub repository for complete MCU, DCEU, and Star Wars configurations!

## Season Support (New in v0.4.0)

You can now specify individual seasons for TV shows! This is useful when different seasons appear at different points in the chronological timeline.

### How to Use Seasons

Add the `season` property to any episode item:

```json
{
  "providerId": "84958",
  "providerName": "tmdb",
  "type": "episode",
  "season": 1
}
```

### Example: Separating Seasons

```json
{
  "universes": [
    {
      "key": "mcu",
      "name": "Marvel Cinematic Universe",
      "items": [
        {"providerId": "299534", "providerName": "tmdb", "type": "movie"},
        {"providerId": "84958", "providerName": "tmdb", "type": "episode", "season": 1},
        {"providerId": "91363", "providerName": "tmdb", "type": "episode", "season": 1},
        {"providerId": "533535", "providerName": "tmdb", "type": "movie"},
        {"providerId": "84958", "providerName": "tmdb", "type": "episode", "season": 2},
        {"providerId": "91363", "providerName": "tmdb", "type": "episode", "season": 2}
      ]
    }
  ]
}
```

In this example:
- Avengers: Endgame (movie)
- Loki Season 1 (episode with season 1)
- What If...? Season 1 (episode with season 1)
- Deadpool & Wolverine (movie)
- Loki Season 2 (episode with season 2)
- What If...? Season 2 (episode with season 2)

### Without Season Number

If you don't specify a season, the entire series will be included:

```json
{"providerId": "84958", "providerName": "tmdb", "type": "episode"}
```

This will include all seasons of the show.

## For Docker Users

If you're running Jellyfin in Docker:

**Option 1: Edit on your computer (if you have a volume mounted)**
```bash
notepad C:\path\to\jellyfin\config\timeline_manager_config.json
```

**Option 2: Copy out, edit, copy back**
```bash
docker cp jellyfin:/config/timeline_manager_config.json ./config.json
# Edit the file
docker cp ./config.json jellyfin:/config/timeline_manager_config.json
```

## Troubleshooting

### File not loading?
1. Check the file is at `/config/timeline_manager_config.json`
2. Validate your JSON at [jsonlint.com](https://jsonlint.com) — one missing comma breaks everything!
3. Check Jellyfin logs for error messages

### Movies/shows not found?
1. Make sure the TMDB/IMDB ID is correct
2. Check that the movie/show is actually in your Jellyfin library
3. Try refreshing metadata in Jellyfin (right-click → Refresh Metadata)

### Still having problems?
- Visit [GitHub Issues](https://github.com/ngdtam/jellyfin-timeline-plugin/issues)
- Check Jellyfin logs for error messages
- Ask for help (include your config file, but remove any personal info!)

## Tips

1. **Start small** — Test with 2-3 movies first before adding everything
2. **Check your commas** — JSON is picky! Every item needs a comma except the last one
3. **Keep a backup** — Save a copy of your working configuration
4. **Use the examples** — Copy from the `configurations/` folder and modify

That's it! You're ready to create your chronological playlists. Happy watching! 🎬
