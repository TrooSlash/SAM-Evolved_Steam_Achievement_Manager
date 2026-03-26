# Changelog

All notable changes to SAM Evolved are documented in this file.

---

## [1.2.0] — 2026-03-26

### Added
- **Unit test project** (SAM.Tests) with 56 tests covering XP formula, schedule window, playtime formatting, VDF parser, and permission bitmask
- **Keyboard shortcuts**: Ctrl+R / F5 (refresh), Ctrl+F (search), Ctrl+S (settings / save)
- **Lock All confirmation dialog** in the achievement editor
- **Schema scan progress indicator** in the status bar
- **Alternating row backgrounds** in list view mode
- **Increased row height** (40px) in list view for better readability
- Localized profile panel status text (7 strings EN+RU)

### Changed
- **Design system consolidation**: All colors centralized in shared `DarkPalette` class, compile-linked across projects — single source of truth for the entire theme
- **LogoDownloadService** extracted from GamePicker (~200 lines) into a dedicated event-based service with parallel download limiting
- **AssemblyResolve handler** shared via `SAM.API/Bootstrap.cs` — eliminates duplicate code between SAM.Picker and SAM.Game
- Removed `TextBright` color alias (was identical to `Text`)
- Replaced 14 hardcoded colors with semantic DarkTheme tokens
- Standardized font sizes across all dialogs (eliminated 7.5F, 8.25F, 8.5F variants)
- Fixed SettingsDialog vertical spacing (uniform 16px) and label-to-control baseline alignment
- List-mode icon size: 32x32 → 36x36

### Fixed
- **Memory leak**: ProfilePanel.SetAvatar() now disposes previous Bitmap before replacing
- **Memory leak**: Manager.OnIconDownload() MemoryStream no longer leaks
- **Layout**: Achievement ListView columns no longer overflow (740px in 682px container)
- **Native memory leak**: SteamApps001.GetAppData AllocHGlobal wrapped in try-finally
- **Pipe handle leak**: Client.Initialize() releases SteamPipe handle on partial failure
- **HTTP timeouts**: All WebClient calls now have a 10-second timeout (was infinite)
- Achievement ListView no longer uses hardcoded Black/White colors
- Playtime formatting now uses InvariantCulture (consistent "1.5 hrs" regardless of locale)

---

## [1.1.1] — 2026-03-24

### Added
- **Protected achievements auto-detection** by scanning Steam's local schema files (`UserGameStatsSchema_*.bin`)
- Lock icon in game list for games with protected achievements
- Visual markers in achievement editor: lock overlay, golden text, tooltip
- "Hide protected" filter button in toolbar
- Full warning dialog when all achievements are protected
- Schema scan results cached in `lib/protected_cache.txt` (7-day expiry)

### Fixed
- False positive protected detection on games like Archeblade and Blackwake (parser now checks `bits` subtrees only)
- Double warning dialog on fully-protected games (callback + callresult dedup)
- Cache cross-process safety via Named Mutex
- Atomic cache writes (temp file + rename)
- Binary KV parser: recursion depth limit (64), WideString support, EOF boundary checks
- Game list set comparison for background scanner

---

## [1.1.0] — 2026-03-22

### Added
- **File-based logging** (Serilog) with daily rotation, 10 MB limit, 7-day retention
- Log level configurable in Settings (Debug / Information / Warning / Error)
- "Open Logs" button in Settings
- **API key encryption** using Windows DPAPI (`ENC:` prefix in settings file)
- Crash protection: unhandled exceptions logged before exit

### Fixed
- GDI+ font handle leak in profile panel (created on every repaint)
- Bitmap leak when game icon downloaded for removed game
- UI deadlock during parallel icon downloads
- Crash in tile view when scrolling during list updates
- XP progress bar calculation (now matches Steam profile)
- Idle mode: visual flickering, schedule boundary off-by-one, status text
- Steam Web API: replaced regex JSON parsing with Newtonsoft.Json

---

## [1.0.1] — Initial Fork Release

### Added (vs. original SAM)
- Steam Web API integration (optional API key)
- Profile panel: avatar, nickname, status, country, level, XP, badges
- Per-game achievement progress column
- Global achievement unlock percentages
- VAC / anti-cheat protection detection and warnings
- Three-phase parallel loading (local → XML types → API enrichment)
- 6 idle modes: Simple, Sequential, Round-Robin, Target Hours, Schedule, Anti-Idle
- Active Games Manager window
- Playtime display from local Steam files
- Tile and List view modes with sortable columns
- Batch game selection (up to 32 games)
- English and Russian localization
- 6 concurrent logo downloads, 8 concurrent API requests
- Image caching across refresh
- Graceful idle shutdown via named events
- Manifest cleanup after idle
- `--unlock-all` CLI argument
- Dark theme (modern 2026 palette)
