# Structural and complexity refactor

**Why:** Players see no change. Splitting oversized multi-responsibility files and decomposing deeply nested builder methods makes the engine and app easier to read, test, and review ahead of open-sourcing the repository.
**Players:** everyone (no visible, stored, or shared behavior changes).

## Plan and risk boundary

1. **`src/EqlPathfinder.Engine/Analysis/Campaign.cs` (~1,672 lines):**
   - Move the 26 public plan/view/combo/option data models into `CampaignModels.cs` with identical namespace, property order, and JSON attributes.
   - Extract `ComboPlacement` (`ComboPlacement.cs`, `internal` with `InternalsVisibleTo` for `EqlPathfinder.Engine.Tests`) to own Must/Unlock-now slot scheduling (`ComboSlot`, `TakeDueSlot`), trio admissibility (`Allowed`), free-combo scoring (`KeptScore`), and the unified Never / held-EveryRun-pair rule (`BreaksHeldRunRule`, `CountBrokenRuns`) shared across `Campaign.Generate`, `Improve`, and `Campaign.Broken` (paying off `docs/debt.md`'s combo-placement item).
   - Split `partial class Campaign` into `Campaign.cs` (core helpers, roster events, `Floor`, `Evaluate`, `History`, `Presets`), `CampaignGenerator.cs` (`Generate` flattened into private builder methods + `Improve`), `CampaignCombos.cs` (`Broken`, `ComboFloor`, `ComboPrice`, `ComboRows`, `ClashOf`), and `CampaignEdits.cs` (`Price`, `DeltaOf`, `Fixes`, `Options`), alongside existing `CampaignFirstLane.cs`.
   - **Flagged risk boundary:** Extracting `ComboPlacement` and flattening `Generate`'s closures preserves exact candidate iteration order, tie-break tolerances, and outputs. Deeper solver unification (merging `Improve`'s `Cost` with `CampaignEdits.Priced`, or changing AA-reset vs played-trio matching) could alter generated plans and is intentionally kept separate from this behavior-preserving refactor.
2. **`src/EqlPathfinder.App/App.xaml.cs` (~1,246 lines):**
   - Split `partial class App` into `App.xaml.cs` (startup/shutdown, tracking, game/log-off watch, window/character actions, auto-updater), `App.Overlays.cs` (fight meter and crawl/meter overlay coordination), `App.Settings.cs` (settings mutations, `SettingsForPage`, campaign/roles/bank/counts persistence, action routing), and `App.SelfTests.cs` (`StartSelfTestIfRequested` + all 8 `SelfTest*Async` developer routines).
3. **`src/EqlPathfinder.Engine/Live/TrackerService.cs` (~1,147 lines):**
   - Move public snapshot DTO records (`NextOption`, `NextStep`, `JourneyView`, `LiveSnapshot`) to `LiveSnapshot.cs`.
   - Split `partial class TrackerService` into `TrackerService.cs` (lifecycle, import, loadout watch, live tailing loop, checkpointing, `Sink`), `TrackerService.Snapshots.cs` (`BuildSnapshot`, `BuildNext`, `PreviewAsCurrent`, pace/verdict caches), `TrackerService.Queries.cs` (History, `ReincarnationData`, Ask, Loot, Roster, `Facts`, `SharePayload`), and `TrackerService.Campaign.cs` (Campaign caches, options, combo pricing, rebuild).
4. **Monolithic pure-engine builders:**
   - Decompose `MeterViews.Build` (`Combat/MeterViews.cs`) into scope resolution (`ResolveScope`), clock/usual timing (`MeasureTiming`), mode builders (`BuildHealing`, `BuildTaken`, `BuildFighterDetail`), `FormatDamageFooter`, and slice row builders (`RowsBySpell`, `RowsByMob`, `RowsByMobFights`, `RowsByEncounter`, `RowsByFighter`).
   - Decompose `AskPage.Build` (`Pages/AskPage.cs`) into normalized context construction and focused section builders (`BuildSources`, `BuildRoute`, `BuildFarm`, `BuildDamage`, `BuildDifficulty`, `BuildPoolSummary`, `Places`).

## Done when
- [x] `Campaign.cs` is split into cohesive files and `ComboPlacement` unifies the Never / held-pair rules across `Generate`, `Improve`, and `Broken` with direct unit tests - `ComboPlacementTests`
- [x] `App.xaml.cs` is split into focused partial files (`App.xaml.cs`, `App.Overlays.cs`, `App.Settings.cs`, `App.SelfTests.cs`) while preserving source-level invariants - `PlannerSwitchTests`, `PlannerOutOfBetaTests`, `AppPageTests`
- [x] `TrackerService.cs` separates `LiveSnapshot.cs` DTOs and splits `TrackerService` into lifecycle, snapshot, query, and campaign partials with identical concurrency and caching - `TrackerServiceTests`, `PageDataTests`
- [x] `MeterViews.Build` and `AskPage.Build` are decomposed into focused helper methods with identical outputs - `FightMeterTests`, `MeterViewsSliceTests`, `AskTests`
- [x] `docs/code-map.md` and `docs/debt.md` match the refactored layout and `tools/check.ps1` passes with 0 warnings and 0 errors - `tools/check.ps1`

## Edge cases
- JSON wire and `settings.json` serialization: record parameter order, property names, and `[JsonIgnore]` attributes on `CampaignModels.cs`, `LiveSnapshot.cs`, `MeterViews.cs`, and `AskPage.cs` remain unchanged.
- Two things at once: `TrackerService` partials share the same `_storeLock`, `_historyLock`, `_gate`, `Volatile` community reference, and `_revision` invalidation without new locks or indirection.
- Source-inspecting tests: `PlannerSwitchTests` and `PlannerOutOfBetaTests` check the split `App*.cs` partials so no plan read/write bypasses `PlannerSwitch`.

## Not doing
- Changing any runtime behavior, planner heuristics (`Cost` vs `Priced`), UI wording, database schema, or `server/` / `site/` code.
- Introducing DI containers, interfaces, or repository layers.

## Where
- `src/EqlPathfinder.Engine/Analysis/`: `Campaign.cs`, `CampaignModels.cs`, `ComboPlacement.cs`, `CampaignGenerator.cs`, `CampaignCombos.cs`, `CampaignEdits.cs`
- `src/EqlPathfinder.App/`: `App.xaml.cs`, `App.Overlays.cs`, `App.Settings.cs`, `App.SelfTests.cs`
- `src/EqlPathfinder.Engine/Live/`: `LiveSnapshot.cs`, `TrackerService.cs`, `TrackerService.Snapshots.cs`, `TrackerService.Queries.cs`, `TrackerService.Campaign.cs`
- `src/EqlPathfinder.Engine/Combat/MeterViews.cs`, `src/EqlPathfinder.Engine/Pages/AskPage.cs`
- `tests/EqlPathfinder.Engine.Tests/`: `ComboPlacementTests.cs`, `MeterViewsSliceTests.cs`, `PlannerSwitchTests.cs`, `PlannerOutOfBetaTests.cs`

## Reviews
- architect: caught `ComboPlacementTests.SpreadOutMustTrioSpacesSlotsAcrossClassRunsWhileUnlockNowQueuesBackToBack` passing `[spread, unlock]`, leaving the `if (first.Unlock)` branch unreached; reordered to `[unlock, spread]` and asserted unconditional back-to-back Unlock-now pinning ahead of `spread`'s `Pos = 0` slot. Verified all splits, JSON contracts, locks, and cache keys match `main`.
