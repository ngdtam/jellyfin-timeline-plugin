# Universal Timeline Manager - Configuration Guide

## Overview

The Universal Timeline Manager plugin creates chronological playlists for cinematic universes (Marvel, DC, Star Wars, etc.) in Jellyfin. Configuration is done via a JSON file that you edit directly.

## Configuration File Location

The configuration file is located at:
```
/config/timeline_manager_config.json
```

**For Docker users:**
```bash
# Access the file in your Jellyfin container
docker exec -it <container-name> cat /config/timeline_manager_config.json

# Or edit it directly on your host if you have a volume mounted
# Example: /path/to/jellyfin/config/timeline_manager_config.json
```

## Configuration Format

### Basic Structure

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

### Field Descriptions

#### Universe Object
- **key** (string, required): Unique identifier for the universe (e.g., "mcu", "dceu", "star-wars")
- **name** (string, required): Display name shown in Jellyfin (e.g., "Marvel Cinematic Universe")
- **items** (array, required): List of movies and TV episodes in chronological order

#### Item Object
- **providerId** (string, required): The TMDB or IMDB ID for the content
- **providerName** (string, required): Either "tmdb" or "imdb"
- **type** (string, required): Either "movie" or "episode"

## How to Find Provider IDs

### Method 1: From TMDB
1. Visit [themoviedb.org](https://www.themoviedb.org)
2. Search for your movie or TV show
3. Look at the URL: `https://www.themoviedb.org/movie/1771` → ID is `1771`

### Method 2: From IMDB
1. Visit [imdb.com](https://www.imdb.com)
2. Search for your content
3. Look at the URL: `https://www.imdb.com/title/tt0371746/` → ID is `tt0371746`

### Method 3: From Jellyfin
1. Right-click on the movie/episode in Jellyfin
2. Select "Edit Metadata"
3. Go to the "External IDs" tab
4. Copy the TMDB or IMDB ID

## Complete Examples

### Marvel Cinematic Universe (MCU)

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
          "type": "movie",
          "note": "Captain America: The First Avenger (1942)"
        },
        {
          "providerId": "10138",
          "providerName": "tmdb",
          "type": "movie",
          "note": "Iron Man (2010)"
        },
        {
          "providerId": "1399",
          "providerName": "tmdb",
          "type": "episode",
          "note": "Agents of S.H.I.E.L.D. - Season 1"
        }
      ]
    }
  ]
}
```

### DC Extended Universe (DCEU)

```json
{
  "universes": [
    {
      "key": "dceu",
      "name": "DC Extended Universe",
      "items": [
        {
          "providerId": "209112",
          "providerName": "tmdb",
          "type": "movie",
          "note": "Man of Steel"
        },
        {
          "providerId": "209112",
          "providerName": "tmdb",
          "type": "movie",
          "note": "Batman v Superman"
        }
      ]
    }
  ]
}
```

### Multiple Universes

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
        }
      ]
    },
    {
      "key": "dceu",
      "name": "DC Extended Universe",
      "items": [
        {
          "providerId": "209112",
          "providerName": "tmdb",
          "type": "movie"
        }
      ]
    },
    {
      "key": "star-wars",
      "name": "Star Wars Saga",
      "items": [
        {
          "providerId": "11",
          "providerName": "tmdb",
          "type": "movie"
        }
      ]
    }
  ]
}
```

## API Endpoints

The plugin provides REST API endpoints for validation and management:

### Test API Connection
```
GET http://localhost:8096/Timeline/Test
```

### Get Current Configuration
```
GET http://localhost:8096/Timeline/Config
```

### Validate Configuration
```
POST http://localhost:8096/Timeline/Validate
Content-Type: application/json

{
  "jsonContent": "{\"universes\":[...]}"
}
```

**Response:**
```json
{
  "isValid": true,
  "message": "✓ Configuration is valid! All 10 items found in your Jellyfin library.",
  "errors": []
}
```

**Or if items are missing:**
```json
{
  "isValid": false,
  "message": "Found 5/10 items in your library. 5 items are missing.",
  "errors": [
    "✗ Marvel Cinematic Universe: movie with tmdb:1771 NOT FOUND in your Jellyfin library",
    "✗ Marvel Cinematic Universe: episode with tmdb:1399 NOT FOUND in your Jellyfin library"
  ]
}
```

### Save Configuration
```
POST http://localhost:8096/Timeline/Save
Content-Type: application/json

{
  "jsonContent": "{\"universes\":[...]}"
}
```

## Validation

The plugin validates your configuration against your actual Jellyfin library:

✅ **Checks if content exists** - Verifies each Provider ID exists in your library
✅ **Reports missing items** - Lists exactly which items are not found
✅ **Validates JSON syntax** - Ensures proper JSON formatting
✅ **Validates structure** - Checks required fields are present

## Troubleshooting

### Configuration Not Loading
1. Check file location: `/config/timeline_manager_config.json`
2. Verify JSON syntax using a validator like [jsonlint.com](https://jsonlint.com)
3. Check Jellyfin logs for errors

### Items Not Found in Library
1. Verify the Provider ID is correct (TMDB/IMDB)
2. Ensure the content is actually in your Jellyfin library
3. Check that metadata has been refreshed in Jellyfin
4. Use the Validate API endpoint to see which items are missing

### Playlist Not Created
1. Ensure configuration is valid
2. Check that at least one item from the universe exists in your library
3. Restart Jellyfin after configuration changes

## Docker Users

### Edit Configuration File

```bash
# Method 1: Edit directly in container
docker exec -it jellyfin-container vi /config/timeline_manager_config.json

# Method 2: Copy out, edit, copy back
docker cp jellyfin-container:/config/timeline_manager_config.json ./timeline_config.json
# Edit the file locally
docker cp ./timeline_config.json jellyfin-container:/config/timeline_manager_config.json

# Method 3: Use volume mount (if configured)
# Edit directly on host at your mounted config path
nano /path/to/jellyfin/config/timeline_manager_config.json
```

### Restart Jellyfin
```bash
docker restart jellyfin-container
```

## Best Practices

1. **Start small** - Begin with a few items and test before adding everything
2. **Use validation** - Always validate before saving to catch errors early
3. **Backup your config** - Keep a copy of your working configuration
4. **Use comments** - While JSON doesn't support comments, you can add a "note" field for reference
5. **Chronological order** - List items in the order you want them to appear in the playlist

## Support

If you encounter issues:
1. Check the [GitHub Issues](https://github.com/ngdtam/jellyfin-timeline-plugin/issues)
2. Validate your configuration using the API endpoint
3. Check Jellyfin logs for error messages
4. Provide your configuration (with sensitive data removed) when reporting issues
