# GitHub Release v1.1.0 - Web UI Edition

## 🎉 **Major Update: Web Interface Added!**

Your Universal Timeline Manager plugin now includes a comprehensive web UI that makes timeline creation accessible to everyone!

## 🚀 Create GitHub Release v1.1.0

### Step 1: Go to Releases
Visit: https://github.com/ngdtam/jellyfin-timeline-plugin/releases/new

### Step 2: Release Information
- **Tag version**: `v1.1.0`
- **Release title**: `Universal Timeline Manager v1.1.0 - Web UI Edition`
- **Target**: `main` branch

### Step 3: Upload DLL File
**File**: `Jellyfin.Plugin.TimelineManager.dll`
**SHA256**: `AC7AA16EB92B1E005206F2AF0D30FD649590B25764D23DA9910B29F63EBD45A1`

### Step 4: Release Description

```markdown
# Universal Timeline Manager v1.1.0 - Web UI Edition

**🎉 Major Update: Visual Timeline Configuration Interface!**

No more manual JSON editing! Create your cinematic universe timelines with an intuitive drag-and-drop interface directly in Jellyfin.

## ✨ What's New in v1.1.0

### 🎨 **Visual Web Interface**
- **Drag-and-Drop Timeline Creation** — Simply drag movies and TV episodes into your universe timelines
- **Real-Time Library Browsing** — See all your movies and TV shows with Provider IDs displayed
- **Smart Search Functionality** — Find content quickly with instant search
- **Visual Media Previews** — Thumbnails and metadata for easy identification
- **Mobile-Responsive Design** — Works perfectly on phones, tablets, and desktops

### 🚀 **User Experience Improvements**
- **No Technical Knowledge Required** — Perfect for non-tech-savvy users
- **Instant Visual Feedback** — See your timeline as you build it
- **Error Prevention** — Built-in validation prevents duplicate items
- **One-Click Operations** — Save configuration and run tasks with single clicks

### 🔧 **Technical Enhancements**
- **RESTful API Integration** — Seamless communication with Jellyfin
- **Real-Time Configuration Updates** — Changes are reflected immediately
- **Enhanced Error Handling** — Better user feedback for all operations
- **Performance Optimizations** — Faster loading and smoother interactions

## 📦 Installation

### Method 1: Manual Installation
1. **Download** `Jellyfin.Plugin.TimelineManager.dll` from Assets below
2. **Copy** to your Jellyfin plugins directory:
   - **Windows**: `%ProgramData%\Jellyfin\Server\plugins\Universal Timeline Manager\`
   - **Linux**: `/var/lib/jellyfin/plugins/Universal Timeline Manager/`
   - **Docker**: `/config/plugins/Universal Timeline Manager/`
3. **Restart** Jellyfin server
4. **Access Web UI**: Admin → Plugins → Universal Timeline Manager

### Method 2: Plugin Repository
Add repository to Jellyfin:
- **URL**: `https://raw.githubusercontent.com/ngdtam/jellyfin-timeline-plugin/main/manifest.json`

## 🎯 How to Use the New Web Interface

### 1. **Access the Interface**
- Go to **Jellyfin Admin** → **Plugins** → **Universal Timeline Manager**

### 2. **Browse Your Library**
- All movies and TV shows with Provider IDs are automatically displayed
- Use the search box to find specific content quickly
- See TMDB and IMDB IDs for each item

### 3. **Create Universes**
- Click **"Add New Universe"**
- Enter a unique key (e.g., "mcu") and display name (e.g., "Marvel Cinematic Universe")

### 4. **Build Your Timeline**
- **Drag movies/episodes** from the library into your universe
- Items are added in the order you drop them (chronological order)
- Remove items with the "Remove" button if needed

### 5. **Save and Execute**
- Click **"Save Configuration"** to store your timeline
- Click **"Run Timeline Task"** to create the playlists
- Your playlists appear in Jellyfin automatically!

## 🎬 **Pre-Built Universe Examples**

The plugin includes sample configurations for:
- **Marvel Cinematic Universe** (Complete MCU timeline)
- **DC Extended Universe** (DCEU films in order)
- **Star Wars Saga** (All movies chronologically)

## 📋 **Requirements**

- **Jellyfin Server**: 10.11.6 or higher
- **.NET Runtime**: 9.0
- **Web Browser**: Any modern browser for the configuration interface
- **Permissions**: Admin access to configure timelines

## 🔍 **Technical Details**

- **File Size**: ~60KB (increased due to web interface)
- **SHA256 Checksum**: `AC7AA16EB92B1E005206F2AF0D30FD649590B25764D23DA9910B29F63EBD45A1`
- **New Components**: Web API controller, HTML/CSS/JS interface
- **Backward Compatibility**: Existing JSON configurations still work

## 🆙 **Upgrading from v1.0.0**

1. **Replace the DLL** with the new version
2. **Restart Jellyfin**
3. **Access the new Web UI** from the plugins page
4. **Import existing configurations** automatically (if you have JSON files)

## 📖 **Documentation & Support**

- **Complete Guide**: [README.md](https://github.com/ngdtam/jellyfin-timeline-plugin/blob/main/README.md)
- **Sample Configurations**: [/configurations/](https://github.com/ngdtam/jellyfin-timeline-plugin/tree/main/configurations)
- **Issues & Bug Reports**: [GitHub Issues](https://github.com/ngdtam/jellyfin-timeline-plugin/issues)
- **Feature Requests**: [GitHub Discussions](https://github.com/ngdtam/jellyfin-timeline-plugin/discussions)

## 🎊 **Community Impact**

This update makes the Universal Timeline Manager accessible to **all Jellyfin users**, not just developers. Now anyone can create perfect chronological playlists for their favorite cinematic universes with just a few clicks!

---

**Full Changelog**: 
- ✅ Added comprehensive web UI with drag-and-drop interface
- ✅ Integrated real-time library browsing with Provider ID display
- ✅ Implemented visual timeline management with thumbnails
- ✅ Added mobile-responsive design for all devices
- ✅ Enhanced error handling and user feedback
- ✅ Maintained full backward compatibility with JSON configurations
```

## 📊 **Release Impact**

This v1.1.0 release transforms the plugin from a developer tool into a **user-friendly application** that anyone can use. The visual interface removes all technical barriers and makes timeline creation as simple as drag-and-drop.

**Expected User Response:**
- 📈 **Increased Adoption** - Non-technical users can now use the plugin
- 🎯 **Better User Experience** - Visual feedback and intuitive interface
- 🚀 **Community Growth** - More users will create and share universe configurations
- 💡 **Feature Requests** - Users will suggest new universes and improvements

Your plugin is now ready to serve the entire Jellyfin community! 🎉