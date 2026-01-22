# Jellyfin Universal Timeline Manager - Distribution Guide

## Overview

This guide explains how to distribute your Jellyfin plugin so users can easily install it.

## Method 1: Official Jellyfin Plugin Repository (Recommended)

### Step 1: Create GitHub Repository
```bash
# Initialize git repository
git init
git add .
git commit -m "Initial commit: Jellyfin Universal Timeline Manager v1.0.0"

# Create GitHub repository and push
git remote add origin https://github.com/your-username/jellyfin-plugin-timeline-manager.git
git branch -M main
git push -u origin main
```

### Step 2: Create Release
1. Go to your GitHub repository
2. Click "Releases" → "Create a new release"
3. Tag version: `v1.0.0`
4. Upload the built DLL file: `Jellyfin.Plugin.TimelineManager.dll`
5. Include release notes from FINAL_VERIFICATION_SUMMARY.md

### Step 3: Submit to Jellyfin Plugin Catalog
1. Fork the [Jellyfin Plugin Repository](https://github.com/jellyfin/jellyfin-plugin-repository)
2. Add your plugin manifest to the appropriate category
3. Submit a Pull Request

**Plugin Manifest Location:**
- File: `manifest.json` in the main repository
- Add your plugin entry to the plugins array

**Required Information:**
- Plugin GUID: `12345678-1234-5678-9abc-123456789012`
- Download URL: Direct link to your DLL file
- SHA256 checksum of the DLL file
- Target Jellyfin version: 10.11.6+

### Step 4: Generate Checksum
```bash
# Windows PowerShell
Get-FileHash -Algorithm SHA256 "Jellyfin.Plugin.TimelineManager.dll"

# Linux/macOS
sha256sum Jellyfin.Plugin.TimelineManager.dll
```

## Method 2: Direct Distribution

### GitHub Releases
Users download the DLL directly from your GitHub releases page.

**Installation Instructions for Users:**
1. Download `Jellyfin.Plugin.TimelineManager.dll` from releases
2. Copy to Jellyfin plugins directory:
   - Windows: `%ProgramData%\Jellyfin\Server\plugins\`
   - Linux: `/var/lib/jellyfin/plugins/`
   - Docker: `/config/plugins/`
3. Restart Jellyfin server
4. Create configuration file at `/config/timeline_manager_config.json`

### Custom Repository
You can host your own plugin repository with a custom manifest.

## Method 3: Community Distribution

### Jellyfin Forum
Post in the [Jellyfin Plugins section](https://forum.jellyfin.org/f-plugins) with:
- Plugin description and features
- Installation instructions
- Download link
- Configuration examples

### Reddit/Discord
Share in Jellyfin community channels:
- r/jellyfin subreddit
- Jellyfin Discord server

## Configuration Distribution

### Sample Configuration Files
Create example configurations for popular universes:

**Marvel Cinematic Universe (MCU):**
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

**Star Wars:**
```json
{
  "universes": [
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

### Configuration Repository
Consider creating a separate repository for community-contributed configurations:
- `configurations/mcu.json`
- `configurations/dc.json`
- `configurations/star-wars.json`

## User Installation Flow

### Via Jellyfin Admin (Official Repository)
1. Jellyfin Admin → Plugins → Catalog
2. Search "Universal Timeline Manager"
3. Click Install
4. Restart Jellyfin
5. Configure universes in `/config/timeline_manager_config.json`

### Manual Installation
1. Download DLL from GitHub releases
2. Copy to plugins directory
3. Restart Jellyfin
4. Create configuration file
5. Run scheduled task

## Documentation

### Wiki/Documentation Site
Consider creating comprehensive documentation:
- Installation guide
- Configuration examples
- Troubleshooting
- API reference
- Community configurations

### Video Tutorial
Create a YouTube tutorial showing:
- Installation process
- Configuration setup
- Running the timeline task
- Viewing created playlists

## Maintenance

### Version Updates
1. Update version in project files
2. Build new release
3. Update plugin manifest
4. Create GitHub release
5. Update documentation

### Community Support
- Monitor GitHub issues
- Respond to forum posts
- Update documentation based on feedback
- Add new universe configurations

## Legal Considerations

### License
Add appropriate license file (MIT, GPL, etc.)

### Attribution
Credit any third-party libraries or inspiration sources.

### Provider ID Usage
Ensure compliance with TMDB/IMDB terms of service for ID usage.

## Next Steps

1. **Create GitHub repository** and push your code
2. **Build release version** and upload DLL
3. **Generate checksum** for the DLL file
4. **Submit to Jellyfin plugin repository** for official distribution
5. **Create sample configurations** for popular universes
6. **Write user documentation** and installation guide

The plugin is production-ready and can be distributed immediately!