# "Rebuild the rest" on a plan that's already cheapest leaves the runs blank

**Built 11-19:** each Campaign edit carries a number, and the page data says the last one the window finished with
(`settings.campaignAnswered`), so the page stops waiting at the answer whether or not the plan changed. From report 14
(0.10.22, Plan → Campaign, the new planner on). Not part of the [campaign-combos](campaign-combos.md) redesign: a bug in
what's already built. **Tier:** small (the lead): one cause, in the page, and the window's half of the campaign
message. Nothing stored or sent off the PC. No new words, so no designer.

**Why:** on Campaign, each run row says what that run earns, and the ones that matter most are gold: a class
reaching 3/3 ("ROG 3/3") and a Completionist rank ("Completionist 2"). After "Rebuild the rest" on a plan a rebuild
doesn't change, those marks go from every row, with the run dots and the XP to level, and "updating…" stays. The
user, 11-19: "I clicked 'Rebuild the rest' - that clears the yellow highlights, and other useful information. And
then the only way to get back was to add or remove a combo". After, the rows are back as soon as the rebuild's
answer comes, whether or not it changed anything.
**Players:** everyone with a campaign who clicks "Rebuild the rest" (or "Build the plan", or changes a goal) when the
plan is already what a rebuild makes. Worst while not playing: no new data comes to clear it.

```text
Plan → Campaign, Your runs (rows from report 14's screenshot)

Before the click, and after the fix                  Today, after "Rebuild the rest"
Your runs   7 played before · 0 done · … · 39 to go  Your runs                            updating…
11  Dark Elf · PAL / WIZ / BST          ●  +20%      11  Dark Elf · PAL / WIZ / BST
    run 11 · PAL 2/3 · WIZ 2/3 · BST 2/3 ·               (no runs, no ⟦Completionist 2⟧, no dot, no XP)
    Dark Elf 1/3 · ⟦Completionist 2⟧
12  Half Elf · ROG / ENC / BER          ●  +30%      12  Half Elf · ROG / ENC / BER
    run 12 · ⟦ROG 3/3⟧ · ⟦ENC 3/3⟧ · ⟦BER 3/3⟧ ·         (no runs, no ⟦ROG 3/3⟧ ⟦ENC 3/3⟧ ⟦BER 3/3⟧, no dot, no XP)
    Half Elf 1/3
```

⟦ ⟧ is gold. The report's screenshot (R2 `2026-11-20/14.png`, kept until about 02-18; the app-feedback skill, step 2)
shows the rows with their gold marks, after they came back. The "Today" side was read from the code, and the page
test showed it before the fix: "updating…" still there after the window's answer.

## The cause (read from the code, 11-19)

- `sendCamp` sets `campLocal` (the page's unsaved copy) and posts the edit. While `campLocal` is set the page is
  `busy`: `campList` draws each row with no runs (no gains, no dots, no XP) and the hero and "Your runs" say
  "updating…".
- `App.SetCampaign` rebuilds, saves and refreshes. `campOnData` drops `campLocal` only when the saved plan differs
  from the one the edit was sent from, or when data arrives more than 5 s after the send.
- A rebuild of a plan that's already cheapest saves the same plan, and its one refresh lands within 5 s, so
  `campLocal` stays. Nothing else clears it until the next data message, and with no play there's none. Adding or
  removing a combo changes the plan, so its answer clears it.
- The same holds for any edit whose answer leaves the plan as it was: "Build the plan" and a goal change that
  rebuilds (`campGoalsChanged`) to the same loadouts.

## Proposal

- The answer to an edit ends the wait, whether or not the plan changed. The page numbers each edit it sends; the
  page data's settings carry the number of the last edit the window finished with (saved, dropped or failed); `campOnData` drops `campLocal` when that number reaches
  the one it sent. A refresh sent before the save still keeps the edit showing (the reason the comparison is there).
- Not a timer alone: a 5 s timer would still show blank rows for 5 s after every no-op rebuild.

## Done when

- [x] "Rebuild the rest" on a plan a rebuild leaves the same: once the answer comes, the runs read exactly as before
  the click (their gold 3/3s, dots and XP), and "updating…" is gone, with no other data message -
  `CampaignEditAnswerTests.RebuildingAPlanThatStaysTheSameShowsItsRunsAgainOnceTheWindowAnswers` (failed before the
  fix on "updating…" still there)
- [x] An answer from before the edit was saved (a refresh already on its way) still keeps the edit showing - the
  same test
- [x] A page opened after the window answered earlier edits numbers its next edit past them, so an old answer never
  ends a new wait - the same test (the fixture's window has answered 3)
- [x] The window fills the number in every page data message - `PageDataTests.EveryPageGetsTheDataMessageItGotBefore`,
  `ASettingTheWindowForgetsToFillDoesNotCompile`

The page test runs the real page in headless Edge with the bug's sequence (the click, a refresh from before the save,
the answer), so it wasn't tried by hand in Chrome as well.

## Edge cases

- Two edits quickly, the first's answer arriving after the second was sent: the number waits for the second's answer
  (the page waits for its last number; the window keeps the highest it finished). A first answer that changes the
  plan still ends the wait through "the saved plan has changed", as before this fix.
- The rebuild throws (`SetCampaign` logs it and refreshes): the number is answered, the wait ends, and the rows show
  the plan as saved.
- An edit dropped because the planner was switched: answered too, beside the "made before you switched" line.
- The window restarts: its number starts at 0 again, and the page's next edit goes past whatever the page last saw.
  A page left open across a restart (it isn't: the page reloads with the window) would still end its wait on the
  plan change or the 5 s.
- A switch flip in flight (`campFlip`) is its own wait and stays as it is.

## Not doing

- Report 13 (same day, same page, about the combos shown): a report of its own, not looked at here.
- Hiding "Rebuild the rest" when a rebuild would change nothing: the button is still honest, and now harmless.

Both planners share `sendCamp` and `campOnData`, so the fix covers the old one too (the test's page is the old
planner's: its data has no `planner`).

## Where

- `src/EqlPathfinder.App/web/js/campaign.js`: `sendCamp` (`campSent`), `campOnData`, `campAnswered`
- `src/EqlPathfinder.Engine/Pages/PageData.cs`: `PageSettings.CampaignAnswered`
- `src/EqlPathfinder.App/App.xaml.cs`: `SetCampaign` (`CampaignAnswered`), `SettingsForPage`
- `src/EqlPathfinder.App/MainWindow.xaml.cs`: the "campaign" message's `n`, `Refresh`

## Reviews

- architect: (11-19) verdict approve, no must fix.
  - Ordering holds. `SetCampaign` stores the plan, sets `CampaignAnswered` and calls `Refresh` with no await between, all
    on the UI thread, and `Refresh` reads settings and plan on that thread before its query. So a message that carries
    `campaignAnswered >= n` always carries the saved plan, and a refresh read earlier carries the old number and keeps
    the edit showing. Saved, dropped and thrown all answer before the refresh.
  - Fine, by reading: two quick edits (the page waits for its last number; `max` keeps the highest; a plan change from
    the first edit's answer still ends the wait, as before, which is the unchanged second condition, so the spec's
    "ends at the second's answer" holds only for answers that leave the plan as it was); a reload (`campSent` restarts at
    0, `campLocal` is null, the next edit goes past `campaignAnswered`); a restart (answered back to 0, the page
    reloads with the window); `SetRoles`/`RebuildAround` (they set `rolesLocal`, cleared on every data message, never
    `campLocal`, so need nothing); old-page and no-`n` messages (n = 0, no change).
  - The tests prove it: the page test runs the real page, the stale refresh keeps "updating…", the answer clears it with
    the same text, the posted `n` is `earlier + 1`, and the second commit only moved the fixture's number to 3 and
    added the "past earlier answers" assertion (meaning kept, sharper). `PageDataTests` holds the new field and the
    required property stops a path that forgets it. The App half (every path sets the number) is read, not run: no
    test reaches `App.SetCampaign`, the same as before this change.
  - Fine, noted: a reload while an earlier edit's rebuild is still running could number the new page's edit the same as
    the one in flight, and the old answer would end the new wait early (the rows show the saved plan until the next
    refresh, a second or so). Needs a reload inside one rebuild; not worth a field.
  - Fine, noted: the test's "keeps waiting" steps rely on the 5 s timer not firing between the click and the stale
    refresh; headless runs take milliseconds, so it holds.
- designer: not needed, the page draws nothing new: after the answer the runs read exactly as they did before the click (the page test compares the whole of Campaign's text), and no word, state or layout is added. The release note is the only new text.
