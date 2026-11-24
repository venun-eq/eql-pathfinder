# A row's menu stays open for a scroll that moved nothing

**Why:** Two page tests failed about 1 run in 5 (`EachClickInTheCardPostsTheListsAndTheGoalsTogether`,
`LeaveItToPathfinderUnranksARaceAndKeepsItsGoal`): the click on the menu's "Leave it to Pathfinder" found nothing, the
menu had closed. The cause is `campaign.js`: `#main`'s `scroll` listener closes the open popover on any scroll event.
A focus (the click's, or `sendCamp`'s refocus after a redraw) scrolls a button into view, and the browser delivers that
event a frame later. In the tests' virtual time frames are rare, so it lands after the next step opened the menu.
Proved: with the listener removed, 34 runs of both classes were green (it failed ~1 in 5 before). Not the harness's
click or its settle wait. A player can hit the same thing: a menu opened right after a redraw or keyboard focus.
**Players:** anyone using Campaign's ⋯ menus (What you want first, Your runs, combos, pickers).

## Done when
- [x] A scroll event that moved nothing leaves the menu open; a scroll that moves its button still closes it -
      `ARowMenuStaysOpenForAScrollThatMovedNothing`
- [x] 50 runs of `CampaignFirstCardEditsTests` and `CampaignRaceGoalsPageTests` are green (50 of 50 green, 11-20)
- [x] The debt.md row for the closing menu is gone

## Edge cases
- Real scroll with the menu open: the button moves, the menu closes as before.
- The button is gone (redrawn away): the menu closes.
- A scroll event before the menu's button is placed: nothing to compare, so it closes as before.

## Where
`src/EqlPathfinder.App/web/js/campaign.js` (the scroll listener, `campRenderPop` remembers where the button was);
`tests/.../PageHarness.cs` gets `PageStep.Scroll()` (a scroll event that moved nothing).

## Reviews
- architect: (11-20) Fix is right and small (`campPopAt` is set at every placement, a missing anchor or no recorded spot closes as before, the 1px slack is in the same CSS pixels at any zoom; Escape, outside click and redraw paths are untouched). One must fix: nothing proves a real scroll still closes the menu, since `ARowMenuStaysOpenForAScrollThatMovedNothing` passes with the listener removed too; add `PageStep.Scroll(by)` (sets `#main.scrollTop` first) and a test that the menu is gone after it.
- engineer: (11-20) Must fix done: `PageStep.Scroll(by)` sets `#main.scrollTop += by` before the event, and the page reads `#main scrollTop`; `ARowMenuClosesWhenARealScrollMovesItsButton` (every race listed so the page is tall) asserts the page really scrolled and the Troll menu is gone. Proved red: with the `#main` scroll listener removed the new test fails; with close-on-any-scroll restored `ARowMenuStaysOpenForAScrollThatMovedNothing` fails. Code put back.
- designer: not needed (11-20), nothing drawn changes: the same menu, it only stops closing by itself; the release note was read by the architect
