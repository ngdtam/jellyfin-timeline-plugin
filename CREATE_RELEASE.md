# GitHub Release Creation Guide

## ✅ Ready for Release!

**Repository**: https://github.com/ngdtam/jellyfin-timeline-plugin
**Manifest URL**: https://raw.githubusercontent.com/ngdtam/jellyfin-timeline-plugin/main/manifest.json ✅

## 🚀 Create Release Now

### Step 1: Go to Releases
Visit: https://github.com/ngdtam/jellyfin-timeline-plugin/releases

### Step 2: Click "Create a new release"

### Step 3: Fill in Release Information
- **Choose a tag**: `v1.0.0` (create new tag)
- **Release title**: `Universal Timeline Manager v1.0.0`
- **Target**: `main` branch

### Step 4: Upload DLL File
**File to upload**: `Jellyfin.Plugin.TimelineManager.dll` (copied to root directory)
**File size**: ~50KB
**SHA256**: `76CF88B9C72AB0DACC8BBA5344639D27D7F76B92A483DE78BD6441CB008F38D2`

### Step 5: Release Description
Copy and paste this description:

```markdown
# Universal Timeline Manager v1.0.0

**Chronological playlists for cinematic universes in Jellyfin.**

Automatically creates and maintains chronological playlists for multiple cinematic universes (Marvel, DC, Star Wars, etc.) based on JSON configuration files.

## 🎯 Key Features

- **Multiple Universe Support** — Configure unlimited cinematic universes in a single JSON file
- **Mixed Content Types** — Movies and TV episodes together in the same chronological playlist  
- **Provider_ID Matching** — Uses TMDB and IMDB identifiers for 100% accurate content matching
- **Error Resilience** — Gracefully handles missing items and service failures without stopping
- **Performance Optimized** — O(1) lookup performance using dictionary structures for large libraries
- **Comprehensive Logging** — Detailed logging with troubleshooting guidance and progress tracking
- **Idempotent Operations** — Safe to run multiple times, updates existing playlists without duplicates

## 📦 Installation

### Method 1: Manual Installation (Recommended)
1. **Download** `Jellyfin.Plugin.TimelineManager.dll` from the Assets section below
2. **Copy** to your Jellyfin plugins directory:
   - **Windows**: `%ProgramData%\Jellyfin\Server\plugins\Universal Timeline Manager\`
   - **Linux**: `/var/lib/jellyfin/plugins/Universal Timeline Manager/`
   - **Docker**: `/config/plugins/Universal Timeline Manager/`
3. **Restart** your Jellyfin server
4. **Create** configuration file at `/config/timeline_manager_config.json`

### Method 2: Plugin Repository
Add this repository to Jellyfin:
- **Dashboard** → **Plugins** → **Repositories** → **Add**
- **Name**: `Universal Timeline Manager`
- **URL**: `https://raw.githubusercontent.com/ngdtam/jellyfin-timeline-plugin/main/manifest.json`

## 🚀 Quick Start

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
        {"providerId": "10138", "providerName": "tmdb", "type": "movie"}
      ]
    }
  ]
}
```

Then:
1. Go to **Dashboard** → **Scheduled Tasks**
2. Find **"Universal Timeline Manager"**
3. Click **"Run Now"**
4. Your playlists will appear in Jellyfin!

## 📋 Requirements

- **Jellyfin Server**: 10.11.6 or higher
- **.NET Runtime**: 9.0
- **Permissions**: Configuration file access and playlist creation

## 🔧 What's Included

- **Main Plugin**: `Jellyfin.Plugin.TimelineManager.dll` (Production build)
- **Sample Configurations**: MCU, DCEU, Star Wars examples in `/configurations/`
- **Documentation**: Complete README with troubleshooting guide
- **Deployment Scripts**: Automated installation helpers

## 🧪 Quality Assurance

This release includes comprehensive testing:
- ✅ **11 Property-based tests** validating correctness properties
- ✅ **Integration tests** for end-to-end workflows  
- ✅ **Error scenario testing** for resilience and recovery
- ✅ **Performance testing** for large media libraries
- ✅ **Production build** with optimizations enabled

## 📖 Documentation & Support

- **Complete Guide**: [README.md](https://github.com/ngdtam/jellyfin-timeline-plugin/blob/main/README.md)
- **Sample Configurations**: [/configurations/](https://github.com/ngdtam/jellyfin-timeline-plugin/tree/main/configurations)
- **Issues & Bug Reports**: [GitHub Issues](https://github.com/ngdtam/jellyfin-timeline-plugin/issues)
- **Feature Requests**: [GitHub Discussions](https://github.com/ngdtam/jellyfin-timeline-plugin/discussions)

## 🎬 Example Universes

The plugin includes sample configurations for:
- **Marvel Cinematic Universe** (Complete timeline)
- **DC Extended Universe** (DCEU films)
- **Star Wars Saga** (All movies in chronological order)

## 🔍 Technical Details

- **File Size**: ~50KB
- **SHA256 Checksum**: `76CF88B9C72AB0DACC8BBA5344639D27D7F76B92A483DE78BD6441CB008F38D2`
- **Target Framework**: .NET 9.0
- **Jellyfin Compatibility**: 10.11.6+

---

**Full Changelog**: Initial release with complete feature set
```

### Step 6: Publish Release
- Check **"Set as the latest release"**
- Click **"Publish release"**

## ✅ After Release

Once published, users can:
1. **Download directly** from GitHub releases
2. **Add your repository** to Jellyfin using the manifest URL
3. **Install via Jellyfin UI** from the plugin catalog

Your plugin will be live and ready for the community! 🎉