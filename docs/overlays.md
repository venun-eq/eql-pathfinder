# Overlays

Pathfinder has a small, chosen set of overlays, decided 11-16. It doesn't have an overlay builder; the Companion app
already does customisable meters and timers. Each overlay answers one question while you play, and all of them behave
the same way.

| Id | Name | What it shows | Its own options |
|---|---|---|---|
| `crawl` | Crawl overlay | This crawl's pace and loot against your usual, and where to go next | `size` (Compact, Standard, Detailed), `mode` (Auto, Leveling, Farming) |
| `meter` | Fight meter | Damage per second by fighter, spell, mob or fight, against your usual here | `scope`, `by`, `mode` (picked on the meter itself) |

## What every overlay shares

`App/OverlayChrome.cs` is attached to each overlay window and does everything that isn't the overlay's own content:
- **Placement:** it remembers where the overlay was, and puts it back on screen if a monitor went away. Drag it to move it.
- **Lock:** clicks pass through to the game. Clicking an overlay never takes focus from the game.
- **Background:** opacity applies to the panel only; the text stays solid.
- **Stays on top:** it stays above a borderless-windowed game.
- **Right-click menu:** every overlay's menu starts with "Open EQL Pathfinder", then the overlay's own items, then Background, Lock, Arrange overlays and Hide.
- **Arranging:** every overlay that's switched on shows (game running or not), unlocked, with a dashed gold outline and its name in a tab. Start and finish it from the tray (Overlays ▸ Arrange overlays… / Done arranging), Settings, or any overlay's menu. Double-clicking the compass also finishes it.

The rules that apply to all overlays live in one place too:
- **Settings:** `AppSettings.Overlays` has one `OverlayPrefs` per id (Visible, Left, Top, Locked, Opacity, Options), plus `OverlaysOnlyWithGame`. Revision 2 of the settings moved the old `Overlay*` and `Meter*` fields there.
- **Tray:** one Overlays submenu, with each overlay's switch, then Lock overlays, Only while EverQuest Legends is running, and Arrange. Double-clicking the compass locks or unlocks every overlay.
- **Settings page:** one Overlays card (web/js/settings.js `overlaysCard`). Its keys are `overlay.<id>.visible|locked|opacity|<option>`, `overlaysOnlyWithGame`, `overlaysLocked` and `arrangeOverlays`, handled by `App.ApplySetting` and `ApplyOverlaySetting`.
- **App:** `App.SetOverlayVisible`, `SetOverlayLocked`, `SetAllOverlaysLocked`, `SetOverlayOpacity`, `SetOverlayOption`, `ToggleOnlyWithGame` and `SetArranging`. `ApplyOverlayVisibility` decides which windows show.

## Adding an overlay

1. Add a `Kind` to `OverlayKinds.All`: id, name, what it's for, starting top, and its options with their defaults.
2. Write a window that draws its content inside a `Border`, creates `new OverlayChrome(this, settings, id, border)`,
   calls `Chrome.Drag()` from its left-button handler, and opens `Chrome.Menu(its own items)` on right-click.
3. In `App`: make the window when tracking starts, return its chrome from `OverlayChromes()`, and give it a line in
   `ApplyOverlayVisibility`. If its options can be set from Settings, add their checks in `ApplyOverlaySetting`
   and the controls to `overlaysCard`.

The tray, the lock-all switch, arranging and the Settings card then cover it with no other changes.

## One trio everywhere

The fight meter labels fights, and works out "your usual", with the app's trio: the /who or spell-checked trio that
every other page uses. That trio comes from `TrackerService.TrioTimeline()`, built from the visits in `PlayFacts`,
which the app hands to `FightMeterService.SetTrioTimeline`. The meter re-labels its stored fights once on its own
thread. A /who the meter saw after the last visit began still wins for the fights after it.
