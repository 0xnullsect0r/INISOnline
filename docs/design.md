# INIS client — UI/UX design

Visual target: the clean, friendly, animated feel of **Catan Universe** ("Catan
Online"), themed with Celtic motifs.

## Design system (`game/ui/theme/`)
- **Palette:** warm parchment + slate, gold/bronze accents; knotwork borders.
- **Godot `Theme` resource** centralizes fonts, colors, button/panel/slider styles
  and focus states — one theme drives every screen.
- **Reusable components:** `PrimaryButton`, `IconButton`, `Card` (hover-zoom, drag),
  `PlayerBanner` (avatar, color, clan count, chieftain/Brenn badges), `Modal`,
  `Toast`, `Tooltip`, `Slider`, `Tabs`, `ConfirmDialog`.
- **Responsive:** anchor/container layouts; desktop (hover, hotkeys) and touch
  (large hit targets, long-press detail, pinch-zoom) variants; iOS safe areas.

## Screen map
Boot → Main Menu → (Account / login·register for online) → Mode Select
(Single-player vs AI · Local Hotseat · LAN host/join · Online) → Friends → Lobby →
Game (HUD) → Results.

### Game HUD
Hex **board** (pan/zoom) · **hand** dock (fan, drag-to-play) · **draft** overlay ·
**clash** panel · **player banners** rail · **phase/turn** indicator + timer ·
**action log** · **chat** · **settings gear** · victory-progress pips
(Leadership/Land/Religion) · end-game results.

## Settings menu (gear → tabbed modal)
- **Audio:** Master / Music / SFX / UI sliders (live), mute.
- **Video:** fullscreen, resolution, V-sync, animation speed, UI scale, colorblind
  clan palette.
- **Gameplay:** confirm-before-commit, auto-pass, tooltip detail, timer display.
- **Account/Session:** profile, friends, leave/resign.
- **Debug Code** button → code entry.
- Persisted to `user://settings.cfg`; audio writes bus volumes immediately.

## Debug / cheat screen
Gear → "Debug Code" → enter `INIS` → cheat panel: view/edit your hand, grant a new
Action or Epic Tale card (picker from the catalogue), adjust deeds (stretch). The
action is sent as a `DebugCommand` and applied **authoritatively** by the host, then
synced to everyone — works in real online games (see `protocol.md`).

## Audio (Phase 8)
Buses `Master → {Music, SFX, UI}`. Royalty-free Celtic music (CC0/CC-BY; attributions
in `credits.md`), menu vs in-game tracks crossfaded. Full SFX set + a menu
button-click. Central `AudioManager` autoload with pooled players, driven by the
engine `Event` stream and UI components.
