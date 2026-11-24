# Campaign's notices go away without moving the page

**Why:** Remove a combo, scroll to look at your runs, and six seconds later the page jumps back to "Add a combo" as
the "Removed" notice goes (report #24). Start over does the same: four seconds after the first click, as "Click again
to clear the plan" turns back into "Start over", the page jumps back up to it. After this, each notice goes away where
it is, and the page stays where you scrolled it.
**Players:** everyone who plans on Campaign. Worst for a player on a short window, where the runs are a long scroll
from the buttons.

## Done when
- [x] The "Removed …" notice goes after 6 seconds, and the page stays where the player scrolled it - `TheRemovedNoticeGoesAwayWithoutScrollingThePageBackToAddACombo`
- [x] "Click again to clear the plan" turns back into "Start over" after 4 seconds, the page stays where it was, and nothing is cleared - `StartOversClickAgainGoesBackWithoutScrollingThePageBackUp`

## Edge cases
- **Two removes within 6 seconds:** the second notice replaces the first and stays its own 6 seconds; the first's
  timer leaves it alone.
- **Pathfinder's answer within 6 seconds:** the page is drawn anew with the notice still on it, as today, and the
  timer takes it away after.
- **Campaign left before the timer:** the notice or the button is changed where it is, on a page that isn't showing;
  coming back draws the page anew without it.
- **Window size and scaling:** the side column comes between the band and the runs below 1150 px, so the runs are
  further from the buttons; the fix is the same in either layout.
- **The keyboard:** the button with the focus keeps it, since nothing is drawn anew.

## Not doing
- The notices' lengths (6 and 4 seconds) and their words.

## Where
- `src/EqlPathfinder.App/web/js/campcombos.js`: `campComboClick`'s remove, the notice in `campCombosCard`
- `src/EqlPathfinder.App/web/js/campaign.js`: the clear click in `campClick`, Start over in `campHero`

## Reviews
- architect: no must or should fix; both timers' stale cases (a second ask, a second remove) checked sound. Logged to
  docs/debt.md: renderCampaign's focus restore still scrolls when Pathfinder's answer lands after a scroll.
- designer: not needed. The same notices with the same words are drawn in the same place; only how they go away changed.
