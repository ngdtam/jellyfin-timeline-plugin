# How to Use Universal Timeline Manager

## Quick Start Guide

### Method 1: Using the Plugin Web UI (Recommended if Python is installed)

1. **Open Jellyfin Web Interface**
   - Go to: `http://localhost:8096`
   - Log in with your admin account

2. **Navigate to Plugin Settings**
   - Click on **Dashboard** (top right)
   - Go to **Plugins** in the left sidebar
   - Click on **Universal Timeline Manager**

3. **Check Python Status**
   - The page will automatically check if Python is installed
   - You'll see one of these messages:
     - ✅ Green: "Ready! Python X.X.X with requests X.X.X detected" → Buttons will appear
     - ⚠️ Yellow: "Python found, but 'requests' library is missing" → Install requests
     - ❌ Red: "Python 3 is not installed" → Use Method 2 below

4. **Create Playlists (if Python is available)**
   - Click **"Create Playlists"** button
   - Wait for validation and execution
   - View results in the output box

### Method 2: Using the Manual Script (Works without Python in Docker)

Since Python is **NOT** installed in your Docker container, use this method:

#### Option A: PowerShell Script (Windows Host)

1. **Open PowerShell on your Windows host**

2. **Run the script:**
   ```powershell
   .\create-timeline-playlists.ps1
   ```

3. **When prompted, enter:**
   - Server URL: `http://localhost:8096`
   - API Key: `6d0a7306edb94f9ea2b1faa512cd4491`

4. **Or run with parameters:**
   ```powershell
   # View help
   Get-Help .\create-timeline-playlists.ps1 -Detailed

   # Run with API key
   .\create-timeline-playlists.ps1 -Server "http://localhost:8096" -ApiKey "6d0a7306edb94f9ea2b1faa512cd4491"

   # Delete existing playlists and recreate
   .\create-timeline-playlists.ps1 -Server "http://localhost:8096" -ApiKey "6d0a7306edb94f9ea2b1faa512cd4491" -DeleteExisting
   ```

#### Option B: Python Script (If you have Python on host)

1. **Install Python dependencies:**
   ```bash
   pip3 install requests
   ```

2. **Run the script:**
   ```bash
   python3 create-timeline-playlists.py --server http://localhost:8096 --api-key 6d0a7306edb94f9ea2b1faa512cd4491
   ```

3. **Delete existing playlists first:**
   ```bash
   python3 create-timeline-playlists.py --server http://localhost:8096 --api-key 6d0a7306edb94f9ea2b1faa512cd4491 --delete-existing
   ```

## Configuration File

The plugin reads from: `/config/timeline_manager_config.json`

**Example configuration:**
```json
{
  "universes": [
    {
      "name": "Marvel Cinematic Universe",
      "items": [
        {
          "type": "movie",
          "tmdb_id": "1771",
          "title": "Captain America: The First Avenger"
        },
        {
          "type": "movie",
          "tmdb_id": "1726",
          "title": "Iron Man"
        },
        {
          "type": "movie",
          "tmdb_id": "24428",
          "title": "The Avengers"
        }
      ]
    }
  ]
}
```

## Testing Your Setup

### Test 1: Verify Plugin is Loaded

```powershell
# PowerShell
$headers = @{"X-Emby-Token" = "6d0a7306edb94f9ea2b1faa512cd4491"}
Invoke-RestMethod -Uri "http://localhost:8096/Timeline/Test" -Headers $headers
```

Expected output: Plugin information

### Test 2: Validate API Key

```powershell
# PowerShell
.\test-api-validation.ps1
```

Expected output:
```
✅ API Key is VALID
Message: API key is valid and working
```

### Test 3: Check Python Availability

```powershell
# PowerShell
.\test-python-availability.ps1
```

Expected output:
```
Python Available: False
Message: ❌ Python 3 is not installed...
```

## Recommended Workflow

### First Time Setup

1. **Verify your configuration file exists:**
   ```powershell
   docker exec jellyfin-win cat /config/timeline_manager_config.json
   ```

2. **Test the API key:**
   ```powershell
   .\test-api-validation.ps1
   ```

3. **Create playlists using PowerShell:**
   ```powershell
   .\create-timeline-playlists.ps1 -Server "http://localhost:8096" -ApiKey "6d0a7306edb94f9ea2b1faa512cd4491"
   ```

4. **Verify playlists in Jellyfin:**
   - Open Jellyfin web UI
   - Go to **Libraries** → **Playlists**
   - You should see "Marvel Cinematic Universe" (or your configured playlists)

### Updating Playlists

When you update your configuration file:

1. **Delete existing playlists:**
   ```powershell
   .\create-timeline-playlists.ps1 -Server "http://localhost:8096" -ApiKey "6d0a7306edb94f9ea2b1faa512cd4491" -DeleteExisting
   ```

2. **Or delete manually in Jellyfin:**
   - Go to the playlist
   - Click the three dots (⋮)
   - Select "Delete"

3. **Create new playlists:**
   ```powershell
   .\create-timeline-playlists.ps1 -Server "http://localhost:8096" -ApiKey "6d0a7306edb94f9ea2b1faa512cd4491"
   ```

## Troubleshooting

### Issue: "API key validation failed"

**Solution:**
1. Verify your API key in Jellyfin:
   - Dashboard → API Keys
   - Copy the correct key
2. Make sure you're using an admin account's API key

### Issue: "Configuration file not found"

**Solution:**
1. Check if file exists:
   ```powershell
   docker exec jellyfin-win ls -la /config/timeline_manager_config.json
   ```
2. Create the file if missing:
   ```powershell
   docker exec jellyfin-win cat > /config/timeline_manager_config.json
   # Paste your JSON configuration
   # Press Ctrl+D to save
   ```

### Issue: "Items not found in library"

**Solution:**
1. Verify items exist in Jellyfin library
2. Check TMDB IDs are correct
3. Make sure library scan is complete
4. Check item types match (movie vs episode)

### Issue: "Playlist is empty"

**Solution:**
1. This is expected - the plugin creates empty playlists
2. Use the PowerShell or Python script to add items
3. The script uses Jellyfin's HTTP API which works correctly

## Advanced Usage

### Using with Multiple Universes

Your configuration can have multiple universes:

```json
{
  "universes": [
    {
      "name": "Marvel Cinematic Universe",
      "items": [...]
    },
    {
      "name": "Star Wars Timeline",
      "items": [...]
    },
    {
      "name": "DC Extended Universe",
      "items": [...]
    }
  ]
}
```

The script will create one playlist for each universe.

### Scheduling Automatic Updates

Create a scheduled task (Windows) or cron job (Linux) to automatically update playlists:

**Windows Task Scheduler:**
```powershell
# Create a scheduled task that runs daily at 3 AM
$action = New-ScheduledTaskAction -Execute "PowerShell.exe" -Argument "-File C:\path\to\create-timeline-playlists.ps1 -Server http://localhost:8096 -ApiKey 6d0a7306edb94f9ea2b1faa512cd4491"
$trigger = New-ScheduledTaskTrigger -Daily -At 3am
Register-ScheduledTask -Action $action -Trigger $trigger -TaskName "Update Jellyfin Playlists" -Description "Updates timeline playlists daily"
```

## Quick Reference

### PowerShell Commands

```powershell
# Create playlists
.\create-timeline-playlists.ps1 -Server "http://localhost:8096" -ApiKey "YOUR_KEY"

# Delete and recreate
.\create-timeline-playlists.ps1 -Server "http://localhost:8096" -ApiKey "YOUR_KEY" -DeleteExisting

# Test API key
.\test-api-validation.ps1

# Test Python availability
.\test-python-availability.ps1

# Test playlist creation
.\test-playlist-creation.ps1
```

### Python Commands

```bash
# Create playlists
python3 create-timeline-playlists.py --server http://localhost:8096 --api-key YOUR_KEY

# Delete and recreate
python3 create-timeline-playlists.py --server http://localhost:8096 --api-key YOUR_KEY --delete-existing

# Use username/password instead of API key
python3 create-timeline-playlists.py --server http://localhost:8096 --username admin --password admin1234
```

### API Endpoints

```
GET  /Timeline/Test                      - Test if plugin is loaded
GET  /Timeline/Config                    - Get current configuration
POST /Timeline/Validate                  - Validate configuration
GET  /Timeline/ValidateApiKey            - Validate API key
GET  /Timeline/CheckPythonAvailability   - Check Python installation
POST /Timeline/ExecutePlaylistScript     - Execute playlist creation script
POST /Timeline/CreatePlaylists           - Create playlists (legacy, creates empty playlists)
```

## Need Help?

1. Check the logs:
   ```powershell
   docker logs jellyfin-win --tail 100
   ```

2. Review the documentation:
   - `README.md` - General plugin information
   - `CONFIGURATION.md` - Configuration file format
   - `PLAYLIST_CREATION.md` - Playlist creation details
   - `API_VALIDATION_FEATURE.md` - API validation details

3. Test each component:
   - Run `test-api-validation.ps1`
   - Run `test-python-availability.ps1`
   - Check configuration file exists
   - Verify items exist in library
