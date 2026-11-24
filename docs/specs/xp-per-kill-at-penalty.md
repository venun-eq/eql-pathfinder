# "Low XP per kill" only when a kill really gives less

**Why:** report #15 (0.10.23, 11-20): "Is it possible that it's not counting the +20% xp bar by chance here? How can I
be getting less xp per kill at 33?" History marked DRU/BRD/NEC's Kedge Keep D0 33 to 35 "On pace · low XP per kill"
at 2.93% a kill.
**Players:** anyone leveling at the low or high end of the levels they've played a place at.

## What the numbers check found

The +20% was counted. Every earlier Kedge Keep D0 run on this PC was at +20% too (each trio's classes were at rank 2),
so the penalty cancels. The one played at the same levels, SHD/ROG/BER 33 to 35, gave 3.31% a kill: this run gave 11%
less, inside the ±15% band.

The "what this place gives" number was the cause. It drew one line through all 17 runs, levels 31 to 49, and read it at
34: 3.55%, so "low" was anything under 3.02%. XP per kill there falls about 9% a level from 32 to 36 and about 20% from
46 to 49, so a straight line through all of it (−12% a level) reads too high at either end.

## What the player sees

Before: `On pace · low XP per kill`. After: `On pace`. Nothing new is drawn: the same verdict, judged fairly. The same
number is the overlay's XP per kill tick and, times the usual kill speed, its Levels/h tick in a crawl, so those ticks
move with it, nearer the runs at your level.

## Done when

- [x] Each earlier run is still moved along the place's slope to the run's level and put back at its XP penalty, but
  the number is the average of the runs that started within 2 levels of it (kill speed's usual), when there are 3.
  With fewer, all of them, as before - `XpPerKillIsJudgedAgainstTheRunsNearItsLevel`
- [x] The reported run reads normal (expected 3.26%), and a run at 2.5% a kill there still reads low - the same test
- [x] No other verdict changes on this PC's history: of 16 visits with an XP verdict, only the reported one moves (low
  to normal), from `status --dump-history` before and after. The overlay's XP per kill and Levels/h ticks move with the
  expected number (the release note says so)
- [x] docs/pace.md says the new rule

## Edge cases

- **Too little:** under 3 runs within 2 levels, all the place's runs set the number, as before; under 3 runs in all,
  there's no XP verdict, as before.
- **The rules change:** runs at different XP penalties are each put at rank 0 before they're averaged, as before.
- **Far from where the place was played:** more than 2 levels outside its runs, still no XP verdict.

## Not doing

- A curved fit. Taking the height from nearby runs fixes the overshoot at either end with the line already there.
- The remaining "low" at Kedge Keep D0 43 (SHD/ROG/BER, 1.06% after 1.51% at 42): it reads low under both rules.

## Reviews
- architect: no must fix. One should fix: the expected XP per kill also sets the overlay's XP per kill and Levels/h ticks, which the notes didn't say; the release note and this spec now do. It checked that the fallback equals the old line exactly and that the near window is kill speed's.
- designer: reviewed the words only (no render; nothing drawn changes). No must fix. Fine: "What the player sees" and the release note use the game's words (XP per kill, Levels/h, +20% XP to level) and keep rule 10 (the bar is bigger, a kill gives what it did). Should fix, done: the report asked "is the +20% counted?" and the note never answered; its first bullet now says it was counted all along and the line was the mistake. Ideas, not this change: (1) the hover on "low XP per kill" (crawls.js:96, "Less XP per kill than this place gives at that level") shows no numbers, which is why the reporter couldn't check it: give it "2.93% a kill; this place gives 3.26% at 33 to 35, at your +20% XP to level" (rule 1); (2) a player with under 3 runs near a level still gets the old all-runs average (edge case "Too little"), so the bias stays for them until they have 3.
