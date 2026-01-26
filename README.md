# Universal Timeline Manager

**Watch your favorite movie universes in chronological order!**

A Jellyfin plugin that creates playlists for cinematic universes (Marvel, DC, Star Wars, etc.) in timeline order instead of release order.

## Features

- **Visual Playlist Creator** - Build playlists with search and drag-and-drop (no JSON editing required)
- **TMDB Integration** - Search for movies and TV shows not in your library
- **Multi-Universe Management** - Manage multiple universe configurations with Web UI
- **Flexible Search** - Toggle between Jellyfin library and TMDB sources
- **Automatic Migration** - Seamlessly upgrades from single-file to multi-file format
- Support for both movies and TV show seasons
- Works with TMDB and IMDB IDs

## Installation

### Method 1: Plugin Repository (Recommended)

1. Open Jellyfin → **Dashboard** → **Plugins** → **Repositories**
2. Add repository:
   - **Name:** `Universal Timeline Manager`
   - **URL:** `https://raw.githubusercontent.com/ngdtam/jellyfin-timeline-plugin/main/manifest.json`
3. Go to **Catalog** → Install **Universal Timeline Manager**
4. Restart Jellyfin

### Method 2: Manual Installation

1. Download the latest release from [Releases](https://github.com/ngdtam/jellyfin-timeline-plugin/releases)
2. Extract to your Jellyfin plugins directory
3. Restart Jellyfin

## Quick Start

### Option 1: Visual Playlist Creator (Easiest)

1. **Configure TMDB API Key** (optional, for searching movies not in your library):
   - Get a free API key from [TMDB](https://www.themoviedb.org/settings/api)
   - Open: Dashboard → Plugins → Universal Timeline Manager → **Tab 1: Universe Management**
   - Enter your API key in **TMDB Settings** section and click **Save**

2. **Create a Playlist Visually**:
   - Go to **Tab 2: Playlist Creator**
   - Enter playlist key (e.g., `mcu`) and name (e.g., `Marvel Cinematic Universe`)
   - Search for movies/shows using the search bar
   - Click items to add them to your playlist
   - Click **Save Playlist** to create the universe file
   - Click **Create Jellyfin Playlist** to generate the actual playlist

3. **View playlists**: Libraries → Playlists

### Option 2: Manual JSON Configuration

1. **Create universe files** in `/config/universes/`:

**File:** `/config/universes/mcu.json`
```json
{
  "key": "mcu",
  "name": "Marvel Cinematic Universe",
  "items": [
    {"providerId": "1771", "providerName": "tmdb", "type": "movie"},
    {"providerId": "1726", "providerName": "tmdb", "type": "movie"}
  ]
}
```

2. **Open plugin page**: Dashboard → Plugins → Universal Timeline Manager

3. **Select universes** and click **Create Playlists for Selected Universes**

4. **View playlists**: Libraries → Playlists

## Finding Movie/Show IDs

**Option 1: Use the Visual Playlist Creator**
- The plugin's search feature automatically finds IDs for you
- Just search and click to add items

**Option 2: From TMDB**
- Go to [themoviedb.org](https://www.themoviedb.org)
- Search for your movie/show
- Get ID from URL: `themoviedb.org/movie/1771` → ID is `1771`

**Option 3: From Jellyfin**
- Right-click movie/show → Edit Metadata → External IDs tab

## Web UI Features

The plugin provides a modern two-tab interface:

### Tab 1: Universe Management
- **Universe Manager** - View, edit, and delete universe JSON files
- **Create Playlists** - Generate Jellyfin playlists from selected universes
- **Manage Playlists** - View and delete existing playlists
- **TMDB Settings** - Configure your TMDB API key for external search

### Tab 2: Playlist Creator
- **Visual Builder** - Create playlists without editing JSON
- **Search Toggle** - Switch between Jellyfin Library and TMDB sources
- **Drag & Drop** - Reorder items in your playlist
- **Live Preview** - See your playlist as you build it
- **One-Click Save** - Save to JSON and create Jellyfin playlist

## Updating Playlists

### Automatic Updates (v0.8.0+)

The plugin includes a scheduled task that automatically updates playlists:

1. Go to Dashboard → Scheduled Tasks
2. Find "Update Timeline Playlists" under "Universal Timeline Manager"
3. Configure schedule (default: Daily at 3:00 AM)
4. Enable/disable as needed
5. Click "Run Now" to trigger manually

The task will:
- Process all universe files in /config/universes/
- Recreate playlists with current library content
- Log detailed information about updates
- Continue even if some universes fail

### Manual Updates

When you add new movies/shows to your Jellyfin library:

1. Go to Dashboard → Plugins → Universal Timeline Manager
2. Select the universes you want to update
3. Click **Create Playlists for Selected Universes**
4. The plugin will recreate the playlists with any newly available content

## Configuration Examples

Check the `configurations/` folder for ready-to-use examples:
- Marvel Cinematic Universe
- Star Wars
- DC Extended Universe

## Documentation

- **[HOW_TO_USE.md](HOW_TO_USE.md)** - Complete usage guide
- **[CONFIGURATION.md](CONFIGURATION.md)** - Configuration file details
- **[DEPLOYMENT_GUIDE.md](DEPLOYMENT_GUIDE.md)** - Development and deployment

## Requirements

- Jellyfin Server 10.10.0 or newer
- (Optional) TMDB API key for searching external content - Get one free at [TMDB](https://www.themoviedb.org/settings/api)

## Changelog

### v0.8.3 (Latest)
- Fixed scheduled task user ID resolution
- Task now finds user ID from existing playlists
- Requires at least one playlist to exist before scheduled task runs

### v0.8.2
- Fixed scheduled task TypeLoadException error
- Removed IUserManager dependency
- Playlists created by scheduled task are now public

### v0.8.1
- Fixed API endpoint URLs for Jellyfin instances with custom base paths
- Resolves errors when accessing plugin from external servers
- All JavaScript fetch calls now use proper URL construction

### v0.8.0
- Scheduled task for automatic playlist updates
- Appears in Dashboard > Scheduled Tasks
- Default schedule: Daily at 3:00 AM (customizable)
- Manual trigger and cancellation support
- Progress reporting and detailed logging

### v0.7.5
- Added TMDB API search integration
- Visual Playlist Creator with search functionality
- User-configurable TMDB API key
- Two-tab UI reorganization
- Search toggle between Jellyfin and TMDB sources

### v0.7.0
- Visual playlist creator (no JSON editing required)
- Content search by title
- Add/remove/reorder playlist items in UI

### v0.6.0
- Multi-universe configuration system
- Web UI for universe management
- Automatic migration from single-file format
- Selective playlist creation

## Roadmap

Planned features for future releases:

- **Auto-Refresh on Library Changes** - Detect new library content and update playlists automatically
- **Playlist Templates** - Pre-configured universe templates for popular franchises
- **Advanced Filtering** - Filter by genre, rating, release year, etc.
- **Playlist Statistics** - View analytics about your timeline playlists

## Support

- **Issues:** [GitHub Issues](https://github.com/ngdtam/jellyfin-timeline-plugin/issues)
- **Examples:** Check `configurations/` folder
- **Documentation:** See HOW_TO_USE.md and CONFIGURATION.md

## License

MIT License
