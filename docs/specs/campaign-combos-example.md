# An empty Your combos offers one to try, and a fresh plan holds none

**Built 11-20, behind the beta switch: an empty Your combos offers one to try (the trio played most, else a pair from
the next run), Add this opens Add a combo filled in, nothing stored until the player adds it** (draft 11-19,
[campaign-page-redesign](campaign-page-redesign.md)). The `designer` goes first, then
the lead builds it, then the `architect` and `designer` review. **Tier:** small with the designer: the empty card's
words and one button that opens Add a combo filled in; nothing stored until the player adds it. **Built behind** the
beta switch. **Waits for:** campaign-page-layout (the card's place). **Unblocks:** campaign-synergies, which can
later supply the example.

**Why:** a fresh plan's Your combos says "Nothing here yet. A combo is a rule you write once: two classes together or
apart, or a trio to play once, twice or three times." The user finds the card good, and asked whether a fresh plan
should come with one combo already in it, "so it's clear how combos work (say, Never SHM with ENC). Maybe. Weigh it
against the zero-state rule above and recommend one way." After, a fresh plan still holds no combo (rule 23), and the
empty card offers one to try: one click opens Add a combo with it filled in and its price shown, as any combo is added.
**Players:** everyone on the beta with a campaign; most of all a new player who hasn't met combos.

```text
Your combos, empty (mock-up: page-b-full.png, the side column's last card)

Today                                             After
Nothing here yet. A combo is a rule you write     Classes you want together or apart, or a trio to play. …
once: … Every rebuild and goal change keeps it.   [Add a combo]  or try one: (Prefer · SHD / ROG / SHM, you've played it 3 times)  Add this
Also kept where free: one strong slow per trio …
[Add a combo]  Or a loadout's ⋯ → Make this a combo.
```

## Weighed: a combo already in the plan, against one offered

- **Already in it** shows how combos work with no click, but it's a choice the player didn't make: it breaks rule 23,
  and a Must or a Never in it is held above the cheapest path, so it could cost runs nobody asked for (rule 8).
- **"Never SHM with ENC"** in particular is already Pathfinder's own, kept where it's free ("Also kept where free: one
  strong slow per trio, from Class roles"). As a Never it would be held at a price; as a Prefer it would add nothing.
  campaign-your-combos took that row out for the same reason.
- **The user chose offered, not stored (11-20, "I agree with all of your picks").** The example teaches the same thing
  and stays out of the plan until a click.

## What the player sees, in short (the designer writes the words)

- "or try one:" and the example as a combo chip, then **Add this**. Add this opens Add a combo filled in, with its
  price line, so the player sees the price before it's in the plan (rule 8). Nothing else changes until they add it.
- **Which example** (the user, 11-20): a trio the player has played twice or more
  (History → Relevels, the played runs campaign-played-trios reads), offered as a Prefer, which costs nothing; with
  none, a pair already together in one of the plan's runs, as a Prefer, so the example is free and changes nothing.
  The mock-up's "Prefer · WAR with CLR" is a placeholder.
- Once the player has a combo, the example goes.

## Where

- `src/EqlPathfinder.App/web/js/campcombos.js`: `campCombosCard` (the empty state), `comboFormHtml` (filled in)
- `src/EqlPathfinder.Engine/Analysis/Campaign.cs`: `History` (the played trios), if the example comes from play

## What the player sees

Only on the beta with the new planner on, and only while **Your combos** is empty. Everything else on the card stays
as it is: the title, "Kept through every rebuild and goal change", the line on Must and Prefer, and "Also kept where
free: one strong slow per trio, from Class roles." (it's Pathfinder's own rule, with the place to turn it off, and it
shows on a full card too). **"Or a loadout's ⋯ → Make this a combo." goes from the empty card**: one hint at a time,
and the example is the better one; on the no-plan page there's no loadout for it to point at. It stays on a card that
has combos.

![The empty card: A with a played trio, B with a pair from the plan, C with neither](.webp)

*Mock-up in the real window, side column at full width (460 px), real plan, made-up example. The captions are the
mock's, not the page's. Changed from the draft's picture: the example is its own dashed box where "Nothing here yet"
was, not on the button's line, because "[Add a combo] or try one: Prefer · … Add this" doesn't fit 460 px; and the
reason sits under the example as a combo row's price line does, so the example looks like the row it would become.*

### A. A trio played twice or more

Where "Nothing here yet…" was, a box with a dashed `--line` border (new: `.combo-try`, 10 px radius, inside the
card's padding):

- Header, small caps `--faint`: **Try one**
- A `.combo-row` without its bottom line: `.kind` **Prefer**, then "Play SHD / ROG / SHM" `.chip` "3 times", then
  `.btn.sm` **Add this** where a real row has its ⋯.
- Under it, the row's `.price` line: "You've played this trio twice (History → Relevels)." ("3 times", "4 times" for
  more.)

Which trio: the one played most, then the one played last. Played means a relevel History → Relevels shows, counted
from the played runs campaign-played-trios reads (before the plan and in it); a run still leveling doesn't count yet.
The combo is a trio, Prefer, Spread out, 3 times: the most a trio can ask for, and as a Prefer it costs nothing.
Shown on the no-plan page too, when the log has one.

### B. No such trio: a pair already together in the plan

The same box: `.kind` **Prefer**, "WAR with CLR" `.chip` "at least once", **Add this**; price line "Together in run 7
already." The pair is two classes of the plan's first run that isn't played (leveling counts), the first two in the
class order the card uses (WAR, CLR, PAL, …); the run number is the one Your runs shows. It changes nothing in the
plan, so it never costs.

### C. Neither (no plan with runs, no trio played twice)

`.combo-none`, as today, with new words: "Nothing here yet. A combo can be a trio you want to play, say 3 times, or two
classes you never want in one run." Then **Add a combo** with no hint beside it.

### Add this

Opens **Add a combo** exactly as the button does, filled in: the classes picked, the controls set (Together; a trio's
Play 3 times and Spread out; a pair's At least once; Prefer), and its price line asked for at once ("Working it out…",
then "What it does: …"). The example box hides while the form is open, as the button row does. Nothing is stored until
**Add combo**. Cancel closes the form, the example comes back, and focus returns to **Add this**. Once the card has a
combo the box is gone, and it comes back only when the last combo is removed.

### Waiting, and narrow

- **A rebuild on its way:** the box keeps what it showed, faded (`.stale`), and comes back clear with the new plan's
  pair or run number (rule 9). It never blanks.
- **Narrow (under 1,150 px):** the side column stacks above Your runs at full width, and the box is that wide; nothing
  wraps differently. At 460 px the row fits on one line: tag, "Play SHD / ROG / SHM", "3 times", Add this.

### Done when

- [x] An empty card with a trio played twice or more shows Try one, Prefer, "Play SHD / ROG / SHM" "3 times", Add this,
  and "You've played this trio twice (History → Relevels)." in the dashed box, and no "Or a loadout's ⋯" hint - seen in
  campaign-combos-try-trio.png
- [x] With no such trio, the box shows a pair from the plan's first run to come and "Together in run N already." -
  seen in campaign-combos-try-pair.png
- [x] With neither, "Nothing here yet. A combo can be a trio you want to play, say 3 times, or two classes you never
  want in one run." and Add a combo alone - seen in campaign-combos-try-none.png
- [x] Add this opens Add a combo with the classes and controls set and its "What it does:" line, the box hidden -
  seen in campaign-combos-try-open.png. The page test proves the form opens with the trio and Cancel brings the box
  back; the controls (Prefer, Together, At least once) and the price line are seen only in the shot, because the test
  page reads text and nothing answers its price question
- [x] Narrow (906 px), the box is full width and its row on one line - seen in campaign-combos-try-narrow.png
- [x] A card with a combo shows no box and keeps "Or a loadout's ⋯ → Make this a combo." - seen in the existing
  combos shot

The page tests (`CampaignCombosPageTests`) prove the pick: played most over played last, then played last; a plan's
done run counts and a leveling run doesn't; the pair from the first run to come with its run number; the words with
neither; no box on a card with a combo.

## Reviews

- **architect:** 11-20: the code follows the spec; the tests didn't prove the pick (most played over last played, a
  done plan run counting, a leveling run not, the run number) or that a card with a combo shows no box. Fixed by the
  lead: the tests now prove each; the form's controls stay with the shot (above). Debt: the pick lives in the page
  script, beside the engine's `History` it reads (docs/debt.md).
- **designer:** 11-20: A, B, the opened form and narrow match the design (try-trio, try-pair, try-open, try-narrow);
  a card with combos shows no box (combos-narrow; its hint is behind the open form there, kept by the unchanged code).
  **Must fix:** try-none.png shows B, not C: on the no-plan page ("Your runs show here once you pick a goal") the box
  says "DRU with BRD · Together in run 8 already.", naming a run the page doesn't list (rule 1), where C says no plan
  with runs means "Nothing here yet…". `comboExample` takes the first not-done run, and a leveling run is there with no
  plan. Fix: offer the pair only when the page lists the plan's runs (a goal is set), keep the trio on the no-plan
  page; a page test for no plan with a leveling run; re-render try-none (the shot's `noTrio` keeps the leveling runs).
  Fixed by the lead: the pair comes only from a page that lists the plan's runs (it has loadouts); the page test's
  no-plan message has a relevel on now; try-none.png re-rendered, and it shows C (seen by the lead).
