# The farm rate and cost per level are values, worked out in one place

**Tier:** small ([numbers-audit](numbers-audit.md)). **Unblocks:** verdicts-out. Pays docs/debt.md's `FarmInfo()` row.

## Why
The farm rate was right only if `FarmInfo()` had stored it in `_options.FarmRate` first, and the cost per level had
three copies with three guards: the overlay (levels/h above 0.05), Ask (above 0, rounded rate, own argument order) and
`BuildNext` (none). The overlay's NEXT and Ask's places could price one level differently.

## Done when
- [x] `TrackerOptions.FarmRate` is gone; nothing stores the rate. `TrackerService.FarmRateNow()` works it out from
  `RouteAnalysis.FarmSource` each time, and `AskInputs.FarmRate`, `LevelingOptions` and `BuildNext` receive it as an argument.
  Proof: `FarmRateValueTests.TheFarmRateIsWorkedOutFromTheSettingEveryTimeNothingNeedsToRunFirst`.
- [x] One `RouteAnalysis.CostPerLevel(farmRate, t5PerHour, levelsPerHour)`, null at 0.05 levels/h or less; `Ask.CostPerLevel` is deleted
  and the overlay, `BuildNext` and Ask's places call it. Proof: `AskNumbersTests` (values, the guard) and
  `FarmRateValueTests.WhereNextAndAskPriceTooSlowALevelTheSameWay`.
- [x] Ask's place rows price with the whole farm rate and the unrounded levels/h, as `BuildNext` does. Proof: `AskPlacesWiringTests`.

## Cases that have bitten
- Penalty: levels/h is divided by the XP penalty once, by the caller (Ask: `b.LevelsPerHour / xp`; `BuildNext`: `60 / (minutes * xp)`); the function takes levels/h as given.
- Null cost: `BuildNext` sorts a null cost last (`?? double.MaxValue`), so a too-slow place never ranks first by cost.
- Cache key: `BuildNext`'s cache key holds the rate in use, so a setting change rebuilds it.

## Where
`Live/TrackerService.cs`, `Analysis/RouteAnalysis.cs`, `Analysis/Ask.cs`, `Pages/AskPage.cs`, `Tracking/EngineState.cs`, `App.xaml.cs`.
Changes numbers: Ask's place rows (Cost) only for places at 0.05 levels/h or less (now blank) and by the rounding of the rate; the overlay's NEXT cost for the same slow places.

## Reviews

- architect: on Sonnet, no must fix; every reader gets the same rate with no call order, the edited Ask tests still prove the rate is used. Should fix: callers that forgot the rate still got 50 silently, and code-map named the deleted `Ask.CostPerLevel`. Both fixed (e6b09fc): `farmRate` is required on `LevelingOptions` and `Group`, the CLI passes it, and `AskInputs` keeps it trailing-optional with the reason beside it.
