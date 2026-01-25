# Deployment Guide

## Quick Deployment

### For Docker Users

```powershell
# Build the plugin
dotnet build Jellyfin.Plugin.TimelineManager/Jellyfin.Plugin.TimelineManager.csproj -c Release

# Copy to Docker container (adjust container name and path as needed)
docker cp Jellyfin.Plugin.TimelineManager/bin/Release/net9.0/Jellyfin.Plugin.TimelineManager.dll jellyfin:/config/plugins/Universal\ Timeline\ Manager_0.5.2.0/

# Restart Jellyfin
docker restart jellyfin
```

### For Manual Installation

1. Build the plugin:
   ```powershell
   dotnet build Jellyfin.Plugin.TimelineManager/Jellyfin.Plugin.TimelineManager.csproj -c Release
   ```

2. Copy the DLL to your Jellyfin plugins directory:
   - **Windows:** `C:\ProgramData\Jellyfin\Server\plugins\Universal Timeline Manager_0.5.2.0\`
   - **Linux:** `/var/lib/jellyfin/plugins/Universal Timeline Manager_0.5.2.0/`

3. Restart Jellyfin

## Web UI Features

After deployment, access the plugin configuration page:

1. Open Jellyfin Dashboard
2. Go to **Plugins** → **Universal Timeline Manager**

You'll see:
- **Create Playlists** button - Creates playlists from your configuration
- **Manage Playlists** section - View and delete existing playlists
- **Configuration File** info - Location and setup instructions

## Clear Browser Cache

If you don't see changes after deployment:

**Hard Refresh:**
- Windows/Linux: `Ctrl + Shift + R`
- Mac: `Cmd + Shift + R`

**Or use Incognito/Private mode**

## Troubleshooting

### Plugin Not Loading
1. Check DLL is in correct directory
2. Verify Jellyfin restarted successfully
3. Check Jellyfin logs for errors:
   ```powershell
   docker logs jellyfin --tail 50
   ```

### Web UI Not Working
1. Clear browser cache (hard refresh)
2. Check browser console (F12) for JavaScript errors
3. Verify you're logged in to Jellyfin

### Playlists Not Creating
1. Verify configuration file exists: `/config/timeline_manager_config.json`
2. Check file has valid JSON syntax
3. Ensure movies/shows exist in your Jellyfin library
4. Check Jellyfin logs for detailed errors

## Configuration File

The plugin looks for: `/config/timeline_manager_config.json`

Example:
```json
{
  "universes": [
    {
      "key": "mcu",
      "name": "Marvel Cinematic Universe",
      "items": [
        {"providerId": "1771", "providerName": "tmdb", "type": "movie"}
      ]
    }
  ]
}
```

See [CONFIGURATION.md](CONFIGURATION.md) for more details.
