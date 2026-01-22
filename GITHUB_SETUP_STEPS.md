# GitHub Setup Steps for Universal Timeline Manager

## Step-by-Step GitHub Repository Creation

### 1. Initialize Git Repository (Local)

```bash
# Navigate to your project directory
cd /path/to/your/jellyfin-plugin-timeline-manager

# Initialize git repository
git init

# Add all files to staging
git add .

# Create initial commit
git commit -m "Initial commit: Universal Timeline Manager v1.0.0

- Complete Jellyfin plugin implementation
- Support for multiple cinematic universes
- Provider_ID matching with TMDB/IMDB
- Mixed content types (movies + TV episodes)
- Comprehensive error handling and logging
- Property-based testing with 11 correctness properties
- Production-ready deployment configuration"
```

### 2. Create GitHub Repository (Web)

1. **Go to GitHub.com** and sign in to your account
2. **Click the "+" icon** in the top right corner
3. **Select "New repository"**
4. **Fill in repository details:**
   - **Repository name**: `jellyfin-plugin-timeline-manager`
   - **Description**: `Chronological playlists for cinematic universes in Jellyfin`
   - **Visibility**: Public (recommended for plugin distribution)
   - **Initialize**: Leave unchecked (we already have files)
5. **Click "Create repository"**

### 3. Connect Local Repository to GitHub

```bash
# Add GitHub remote (replace YOUR_USERNAME with your actual GitHub username)
git remote add origin https://github.com/YOUR_USERNAME/jellyfin-plugin-timeline-manager.git

# Set main branch
git branch -M main

# Push to GitHub
git push -u origin main
```

### 4. Build Release Version

```bash
# Build the plugin in Release configuration
dotnet build Jellyfin.Plugin.TimelineManager/Jellyfin.Plugin.TimelineManager.csproj -c Release

# The DLL will be created at:
# Jellyfin.Plugin.TimelineManager/bin/Release/net9.0/Jellyfin.Plugin.TimelineManager.dll
```

### 5. Create GitHub Release

1. **Go to your repository** on GitHub
2. **Click "Releases"** (on the right side)
3. **Click "Create a new release"**
4. **Fill in release details:**
   - **Tag version**: `v1.0.0`
   - **Release title**: `Universal Timeline Manager v1.0.0`
   - **Description**: Copy from the template below
5. **Upload the DLL file:**
   - Drag and drop `Jellyfin.Plugin.TimelineManager.dll`
   - Or click "Attach binaries" and select the file
6. **Click "Publish release"**

### Release Description Template

```markdown
# Universal Timeline Manager v1.0.0

**Chronological playlists for cinematic universes in Jellyfin.**

## 🎯 Features

- **Multiple Universe Support** — Configure unlimited cinematic universes
- **Mixed Content Types** — Movies and TV episodes in same playlist
- **Provider_ID Matching** — TMDB and IMDB identifiers for accuracy
- **Error Resilience** — Graceful handling of missing items
- **Performance Optimized** — O(1) lookup for large libraries
- **Comprehensive Logging** — Detailed troubleshooting support

## 📦 Installation

### Method 1: Manual Installation
1. Download `Jellyfin.Plugin.TimelineManager.dll` below
2. Copy to your Jellyfin plugins directory:
   - Windows: `%ProgramData%\Jellyfin\Server\plugins\`
   - Linux: `/var/lib/jellyfin/plugins/`
   - Docker: `/config/plugins/`
3. Restart Jellyfin server
4. Create `/config/timeline_manager_config.json` with your universe configurations

### Method 2: Plugin Repository (Coming Soon)
Will be available through Jellyfin's official plugin catalog.

## 🚀 Quick Start

Create `/config/timeline_manager_config.json`:

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

Then run the "Universal Timeline Manager" scheduled task in Jellyfin admin.

## 📋 Requirements

- Jellyfin Server 10.11.6+
- .NET 9.0 runtime
- Configuration file access

## 🔧 What's Included

- **Main Plugin**: `Jellyfin.Plugin.TimelineManager.dll`
- **Documentation**: Complete README with examples
- **Sample Configurations**: MCU, DCEU, Star Wars examples
- **Deployment Scripts**: Automated installation helpers

## 🧪 Testing

This release includes comprehensive testing:
- ✅ 11 Property-based tests validating correctness
- ✅ Integration tests for end-to-end workflows
- ✅ Error scenario testing for resilience
- ✅ Performance testing for large libraries

## 📖 Documentation

See the [README](https://github.com/YOUR_USERNAME/jellyfin-plugin-timeline-manager/blob/main/README.md) for:
- Complete installation guide
- Configuration examples
- Troubleshooting help
- Performance optimization tips

## 🐛 Support

- **Issues**: [GitHub Issues](https://github.com/YOUR_USERNAME/jellyfin-plugin-timeline-manager/issues)
- **Discussions**: [GitHub Discussions](https://github.com/YOUR_USERNAME/jellyfin-plugin-timeline-manager/discussions)
- **Documentation**: [Project README](https://github.com/YOUR_USERNAME/jellyfin-plugin-timeline-manager/blob/main/README.md)

---

**Full Changelog**: Initial release with complete feature set
```

### 6. Generate File Checksum

```bash
# Windows PowerShell
Get-FileHash -Algorithm SHA256 "Jellyfin.Plugin.TimelineManager/bin/Release/net9.0/Jellyfin.Plugin.TimelineManager.dll"

# Linux/macOS
sha256sum "Jellyfin.Plugin.TimelineManager/bin/Release/net9.0/Jellyfin.Plugin.TimelineManager.dll"

# Save this checksum - you'll need it for the plugin manifest
```

### 7. Update Plugin Manifest

Edit `plugin-manifest.json` with your actual details:

```json
{
  "guid": "12345678-1234-5678-9abc-123456789012",
  "name": "Universal Timeline Manager",
  "description": "Creates and maintains chronological playlists for cinematic universes based on JSON configuration files.",
  "overview": "Automatically creates chronological playlists for multiple cinematic universes (Marvel, DC, Star Wars, etc.) with Provider_ID matching and mixed content support.",
  "owner": "YOUR_GITHUB_USERNAME",
  "category": "General",
  "imageUrl": "https://raw.githubusercontent.com/YOUR_GITHUB_USERNAME/jellyfin-plugin-timeline-manager/main/images/logo.png",
  "versions": [
    {
      "version": "1.0.0.0",
      "changelog": "Initial release with multiple universe support, Provider_ID matching, and comprehensive error handling",
      "targetAbi": "10.11.6.0",
      "sourceUrl": "https://github.com/YOUR_GITHUB_USERNAME/jellyfin-plugin-timeline-manager/releases/download/v1.0.0/Jellyfin.Plugin.TimelineManager.dll",
      "checksum": "YOUR_SHA256_CHECKSUM_HERE",
      "timestamp": "2025-01-22T00:00:00Z"
    }
  ]
}
```

### 8. Submit to Jellyfin Plugin Repository (Optional)

1. **Fork** the [Jellyfin Plugin Repository](https://github.com/jellyfin/jellyfin-plugin-repository)
2. **Add your plugin** to the manifest
3. **Submit Pull Request** for official inclusion

## Summary of Commands

```bash
# 1. Initialize and commit
git init
git add .
git commit -m "Initial commit: Universal Timeline Manager v1.0.0"

# 2. Connect to GitHub (replace YOUR_USERNAME)
git remote add origin https://github.com/YOUR_USERNAME/jellyfin-plugin-timeline-manager.git
git branch -M main
git push -u origin main

# 3. Build release
dotnet build Jellyfin.Plugin.TimelineManager/Jellyfin.Plugin.TimelineManager.csproj -c Release

# 4. Generate checksum
Get-FileHash -Algorithm SHA256 "Jellyfin.Plugin.TimelineManager/bin/Release/net9.0/Jellyfin.Plugin.TimelineManager.dll"
```

## Next Steps After GitHub Setup

1. **Create the GitHub release** with the DLL file
2. **Update README links** to point to your actual repository
3. **Test the installation** process yourself
4. **Share with the community** on Jellyfin forums/Discord
5. **Consider submitting** to official Jellyfin plugin repository

Your plugin is now ready for distribution! 🎉