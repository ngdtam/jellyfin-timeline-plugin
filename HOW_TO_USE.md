# How to Use Universal Timeline Manager

## Quick Start Guide

### Step 1: Access the Plugin

1. **Open Jellyfin Web Interface**
   - Go to: `http://localhost:8096` (or your Jellyfin server URL)
   - Log in with your admin account

2. **Navigate to Plugin Settings**
   - Click on **Dashboard** (top right)
   - Go to **Plugins** in the left sidebar
   - Click on **Universal Timeline Manager**

### Step 2: Create Your Configuration File

Before creating playlists, you need a configuration file that tells the plugin which movies/shows to include.

**File location:** `/config/timeline_manager_config.json`

**Example configuration:**
```json
{
  "universes": [
    {
      "key": "mcu",
      "name": "Marvel Cinematic Universe",
      "items": [
        {"providerId": "1771", "providerName": "tmdb", "type": "movie"},
        {"providerId": "1726", "providerName": "tmdb", "type": "movie"},
        {"providerId": "24428", "providerName": "tmdb", "type": "movie"}
      ]
    }
  ]
}
```

**See [CONFIGURATION.md](CONFIGURATION.md) for complete examples and how to find movie/show IDs.**

### Step 3: Create Playlists

Once your configuration file is ready:

1. **Click the "Create Playlists" button** in the plugin page
2. Wait for the success message
3. Your playlists will appear in the **Manage Playlists** section below

**That's it!** The plugin will:
- Read your configuration file
- Find matching movies/shows in your library
- Create playlists with items in chronological order
- Show you any missing items

### Step 4: View Your Playlists

1. Go to **Libraries** → **Playlists** in Jellyfin
2. You'll see your timeline playlists (e.g., "Marvel Cinematic Universe")
3. Click to view and play in chronological order

## Managing Playlists

### View Existing Playlists

The plugin page shows all your timeline playlists with:
- Playlist name
- Number of items
- Delete button

### Delete a Playlist

1. Find the playlist in the **Manage Playlists** section
2. Click the **Delete** button
3. Confirm the deletion

### Update Playlists

When you update your configuration file:

1. Delete the old playlist using the **Delete** button
2. Click **Create Playlists** to recreate with new configuration

## Alternative: Using the API Directly

You can also create playlists using the Jellyfin API:

### PowerShell (Windows)

```powershell
# Login to get your user ID
$login = Invoke-RestMethod -Uri "http://localhost:8096/Users/authenticatebyname" `
  -Method Post `
  -Body '{"Username":"admin","Pw":"yourpassword"}' `
  -ContentType "application/json"

$userId = $login.User.Id
$token = $login.AccessToken

# Create playlists
Invoke-RestMethod -Uri "http://localhost:8096/Timeline/CreatePlaylists?userId=$userId" `
  -Method Post `
  -Headers @{"X-Emby-Token"=$token}
```

### curl (Linux/Mac)

```bash
# Login to get your user ID
curl -X POST "http://localhost:8096/Users/authenticatebyname" \
  -H "Content-Type: application/json" \
  -d '{"Username":"admin","Pw":"yourpassword"}'

# Create playlists (replace USER_ID and TOKEN with values from login)
curl -X POST "http://localhost:8096/Timeline/CreatePlaylists?userId=USER_ID" \
  -H "X-Emby-Token: TOKEN"
```

## Docker Users

If you're running Jellyfin in Docker, you can edit the configuration file:

### Option 1: Edit on Host (if volume is mounted)

```bash
# Windows
notepad C:\path\to\jellyfin\config\timeline_manager_config.json

# Linux/Mac
nano /path/to/jellyfin/config/timeline_manager_config.json
```

### Option 2: Copy Out, Edit, Copy Back

```bash
# Copy from container
docker cp jellyfin:/config/timeline_manager_config.json ./config.json

# Edit with your favorite editor
# Then copy back
docker cp ./config.json jellyfin:/config/timeline_manager_config.json
```

### Option 3: Edit Inside Container

```bash
docker exec -it jellyfin vi /config/timeline_manager_config.json
```

## Troubleshooting

### Issue: "Configuration file not found"

**Solution:**
1. Check if file exists:
   ```bash
   # Docker
   docker exec jellyfin ls -la /config/timeline_manager_config.json
   
   # Local
   ls -la /var/lib/jellyfin/config/timeline_manager_config.json
   ```

2. Create the file if missing (see [CONFIGURATION.md](CONFIGURATION.md) for examples)

### Issue: "Items not found in library"

**Solution:**
1. Verify items exist in your Jellyfin library
2. Check TMDB/IMDB IDs are correct
3. Make sure library scan is complete
4. Refresh metadata for items (right-click → Refresh Metadata)

### Issue: "Failed to create playlist"

**Solution:**
1. Check Jellyfin logs for detailed errors:
   ```bash
   # Docker
   docker logs jellyfin --tail 100
   
   # Local
   tail -f /var/log/jellyfin/jellyfin.log
   ```

2. Verify your configuration file has valid JSON syntax at [jsonlint.com](https://jsonlint.com)
3. Make sure you're logged in as an admin user

### Issue: Web UI not loading

**Solution:**
1. Clear browser cache (Ctrl+Shift+R or Cmd+Shift+R)
2. Try incognito/private mode
3. Check browser console (F12) for JavaScript errors
4. Verify plugin is installed and Jellyfin restarted

## Configuration Examples

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
        {"providerId": "10138", "providerName": "tmdb", "type": "movie"},
        {"providerId": "84958", "providerName": "tmdb", "type": "episode", "season": 1}
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

### Multiple Universes

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

**More examples:** Check the `configurations/` folder in the GitHub repository!

## Tips

1. **Start small** — Test with 2-3 movies first before adding everything
2. **Check your JSON** — Use [jsonlint.com](https://jsonlint.com) to validate syntax
3. **Keep a backup** — Save a copy of your working configuration
4. **Use the examples** — Copy from the `configurations/` folder and modify

## Need More Help?

- **Configuration details:** [CONFIGURATION.md](CONFIGURATION.md)
- **Deployment guide:** [DEPLOYMENT_GUIDE.md](DEPLOYMENT_GUIDE.md)
- **Report bugs:** [GitHub Issues](https://github.com/ngdtam/jellyfin-timeline-plugin/issues)
- **Example configs:** Check the `configurations/` folder

Happy watching! 🎬
