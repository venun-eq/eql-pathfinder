# Motes and fragments

*T5-eq and its place among every number: [calculations.md](calculations.md).*

Motes of Potential and Fragments of Potential are upgrade currency. Both live in currency storage and add
points to an item or a spell. They come in the same 10 tiers. The code version of this page is
`src/EqlPathfinder.Engine/Model/Potential.cs`, and `PotentialTests` checks it against the in-game previews.

| Tier | Name | Usual source |
|---|---|---|
| 1 | Infinitesimal | D0 |
| 2 | Minor | D1 |
| 3 | Lesser | D2 |
| 4 | Potential (no tier word: "Mote of Potential") | D3 |
| 5 | Major | D4 |
| 6–10 | Greater, Superior, Grand, Ascendant, Infinite | D4 rare drops, chests, condensing (motes always; fragments since the 11-17 patch) |

## Tiers cost points

- Going from tier t to t+1 costs **2^t points**, for items and spells alike. Every preview denominator (1/8, 4/8, 7/16, 1/512 …) is 2^tier.
- From +0, reaching +T costs 2^T − 1. From +k to +10 costs 2^10 − 2^k, minus any progress already in tier k.
- Merging a duplicate item at +k adds 2^k, plus any progress it has in its tier: all its points, plus 1. Copies at different +levels merge too (both confirmed by the player, 11-17). The log records it: `You have successfully merged two items together to create a new item: Windstriker +8`. See [Duplicate items](#duplicate-items).
- Void-Touched Potential adds one full tier.

## Points per mote or fragment

| Tier | Mote → spell | Fragment → spell | Mote → gear | Fragment → gear | Gear cap |
|---|---|---|---|---|---|
| 1 Infinitesimal | 1 | 2* | 1 | 2* | +0 |
| 2 Minor | 2 | 4* | 2 | 3* | +1 |
| 3 Lesser | 4 | **8** | 3 | **4** | +2 |
| 4 Potential | 8 | **16** | 4 | **6** | +3 |
| 5 Major | **16** | **32** | **5** | **8** | **+4** |
| 6 Greater | 32 | **64** | 6 | **11** | +5 |
| 7 Superior | 64 | 128* | 7 | **16** | +6 |
| 8 Grand | 128 | **256** | 8 | **22** | +7 |
| 9 Ascendant | 256 | **512** | 9 | **32** | +8 |
| 10 Infinite | 512 | 1024 | 10 | **45** | +9 |

**Bold** = measured with the in-game preview on 11-16 (readings at the bottom). \* = follows the pattern, not
measured. The other mote values come from the eqlwiki Mote Guide and fit the same rules as the measured Major.
"Gear cap" is the highest item tier a mote or fragment can go into: one below its own tier. Motes and fragments
share it (a Fragment of Major was refused by a +5 item, 11-16). The game refuses higher items with
`The item you are trying to add will not work, this mote is not sufficiently powerful to upgrade this item.`
A fragment's gear points don't depend on the item's progress in its tier (11-16).

In words:

- **Spells:** a mote of tier r adds 2^(r−1), and a fragment adds 2^r. So a fragment is worth exactly one mote of the next tier up, at every tier. Spells have no tier cap.
- **Gear:** a mote adds its tier number (Major 5, Grand 8). A fragment adds about ×1.4 more per tier, landing on exact powers of two at odd tiers (Lesser 4, Major 8, Superior 16, Ascendant 32).

## Condensing

At Constructed Potential, 2 motes of one tier become 1 of the next tier. The log records the offer
(`You offered 52 Mote of Major Potential to Constructed Potential.`), and the next offer shows what came back
(52 Major → 26 Greater on 10-27; 100 + 200 Major → 150 Greater on 10-01).

Condensing loses nothing on spells (2 × 2^(r−1) = 2^r) but loses a lot on gear: 2 Majors are 10 gear points,
and the Greater they make is 6.

**Fragments condense too, 2:1, since the 11-17 patch.** Before it they couldn't. The player checked it on 11-17: 2
Fragments of Major gave 1 Fragment of Greater, and the log line is the mote one with `Fragment`:
`You offered 2 Fragment of Major Potential to Constructed Potential.` `MoteBank.Current` moves condensed fragments
up a tier, as it does motes, and `UpgradePlanner` plans fragment condensing (`CondenseStep.Kind`).

Condensing fragments loses nothing on spells (2 × 2^r = 2^(r+1)) and gains on high gear (see
[Fragments on spells or gear](#fragments-on-spells-or-gear)).

## T5-equivalent

T5-eq is **spell points ÷ 16**, so one Major mote = 1. For motes it's also the cost in Majors via condensing.
On that scale a fragment of tier r is 2^(r−4), which is **2.0× a same-tier mote**.

On gear, what a point is worth depends on the item's tier. High items take only high motes, and those cost
more Majors per point:

| Item tier | Cheapest mote that fits | Gear points per Major |
|---|---|---|
| +4 or lower | Major | 5 |
| +5 | Greater | 3 |
| +6 | Superior | 1.75 |
| +7 | Grand | 1 |
| +8 | Ascendant | 0.56 |
| +9 | Infinite | 0.31 |

A fragment's gear value in Majors is its gear points ÷ that rate (`Potential.GearMajors`). Here it is at the
highest item tier each fragment can go into:

| Fragment | On gear at its top tier | On a spell |
|---|---|---|
| Major (+4) | 1.6 | 2 |
| Greater (+5) | 3.7 | 4 |
| Superior (+6) | 9.1 | 8 |
| Grand (+7) | 22 | 16 |
| Ascendant (+8) | 57 | 32 |
| Infinite (+9) | 144 | 64 |

Below its top item tier, a fragment's gear value falls fast: a Fragment of Grand on a +5 item is 22 ÷ 3 ≈ 7
Majors, under half its spell value. Where to spend one now that fragments condense: see
[Fragments on spells or gear](#fragments-on-spells-or-gear).

The single fragment value the app counts loot with (Settings) is in [The ideal split](#the-ideal-split).

## Duplicate items

A merge loses nothing, so a point costs the same in any copy. But a copy at a lower tier still takes cheaper motes.
That makes it cheaper to raise every copy and merge late than to merge early. Two copies at +5 both go to +7 on
Greater and Superior motes and merge into +8 for about 94 T5-eq, where one copy alone needs +7 → +8 on 16 Grand
motes (about 176 in all). Merging right away only saves the +5 tier.

`UpgradePlanner` treats each copy as another item to put points into. Every unit goes into whichever copy is lowest,
and the copies merge in at the end. Picking an item on the Upgrades page lists every copy the inventory export has,
with its +N and where it is. The player types each copy's points within its tier (the export doesn't have them), can
add a copy it's missing (another loadout), and unticks one to leave it out. Every ticked copy merges into the one with
the most points; merging is the default because it's never dearer. The "Merging saves" tile compares against raising
that one alone. A newer export lists the copies again; until then, a +N corrected by hand stays.

## Fragments on spells or gear

Against the motes they replace, a fragment is worth 2.0× on a spell at every tier. On gear, at its top item tier, it's
1.6× (Major), 1.83× (Greater), then 2.3×, 2.75×, 3.6× and 4.5× (Superior to Infinite).

**Before the 11-17 patch** fragments couldn't condense. So with spells and gear both to raise, Major and Greater
fragments saved more on spells, and Superior and up saved more on gear.

**Since the patch, keep every fragment for gear.** Superior and higher fragments drop rarely (17 Greater, 1
Superior, 1 Grand per 311 Major fragments, below), so condensing is the realistic way to get them. A fragment spent on
a spell is one that can't become one of those. At 2:1, a fragment's best use is condensing it to the highest
fragment your gear can take and putting that into an item one tier below it:

| Fragment | On a spell | Gear at its own top tier | Condensed to Infinite, into a +9 item |
|---|---|---|---|
| Major | 2 | 1.6 | 144 ÷ 32 = **4.5** |
| Greater | 4 | 3.7 | 144 ÷ 16 = **9** |
| Superior | 8 | 9.1 | **18** |
| Grand | 16 | 22 | **36** |
| Ascendant | 32 | 57 | **72** |

(Majors per fragment.) Condensed up to its top use, every fragment is worth 2.25× its spell value. The last column
needs a +9 item still to raise. With no gear that high left, the best target is one tier above your highest
unfinished item, and each tier lower roughly halves the gain. Spells come out ahead only once you have no gear left
to raise.

`UpgradePlanner.FragmentCharge` prices a fragment at its best use, condensing included, as high as the recommendation
feeds gear (`Potential.RecommendedGoal`: +9, with VTPs saved for +9 → +10; see [The ideal split](#the-ideal-split)):
an Ascendant fragment into a +8 item, about 56.9 Majors, so 56.9 ÷ 2^(9 − tier) from Lesser up (3.56 a Major fragment).
An Infinite is priced as the two Ascendants it's made from (113.8), so the charge still doubles tier by tier.
Infinitesimal and Minor fragments are worth more on a +0 or +1 item (0.4 and 0.6) than condensed to an Ascendant.
At +8 → +9 a fragment costs exactly what it saves, so the plan uses it there when that wastes no more points than motes
would. Where a plan has no VTP for +9 → +10, fragments condensed to Infinite are worth more there (144 for 113.8), so
they go there first. It plans the condensing (`CondenseStep` with `Kind = Fragment`), using the highest fragments
first. Lower down and on spells it leaves fragments alone. There's no "spells to rank" switch any more (0.9.36 had one).
Until 11-17 the charge stopped at Infinite into a +9 item (144 ÷ 2^(10 − tier), 4.5 a Major), which held fragments
back for a +9 → +10 that a VTP was going to do.

## The ideal split

Both currencies come from the same farming, and fragments are the scarce one: about 1 Major fragment per 13 Major
motes looted ([How rare each tier is](#how-rare-each-tier-is)). Farming is least when each fragment goes where it beats a
mote by most: comparative advantage. Per Major spent, with condensing on both sides (motes condensed from Majors,
fragments from Major fragments):

| Where the points go | Mote: points per Major | Fragment: points per Major fragment | Fragment ÷ mote |
|---|---|---|---|
| Gear +9 → +10 | 0.31 | 1.41 | **4.5** |
| Gear +8 → +9 | 0.56 | 2.0 | 3.56 |
| Gear +7 → +8 | 1.0 | 2.75 | 2.75 |
| Gear +6 → +7 | 1.75 | 4.0 | 2.29 |
| **Spells** | 16 | 32 | **2.0** |
| Gear +5 → +6 | 3.0 | 5.5 | 1.83 |
| Gear +4 or lower | 5.0 | 8.0 | 1.6 |

Fragments fill from the top down; motes take everything below where they run out. So:

- **Fragments never go on spells** while some mote-fed item still needs +6 or higher.
- **Motes take an item to +6** (through the +5 tier, not to +5): at +5 → +6 a fragment beats a mote by less than on a spell.
- **Fragments go to the top first.** One item's +9 → +10 takes about 364 Major-fragment equivalents (512 points at
  1.41), and +6 → +10 about 555, against about 311 Major fragments looted by 11-17. So in practice every fragment
  belongs in +9 → +10, and motes pay for +6 to +9 too. 311 Major fragments on one item going +5 → +10 save about
  1,400 Majors condensed to Infinite for +9 → +10, about 1,160 spent from +6 up as it climbs, and about 620 on spells.
- Only when someone's fragments outgrow the +9 demand of the gear they'll feed do the extras step down: +8, +7, +6,
  then spells.

Which gear gets fed is the player's choice: gear that drops easily (planar crawl gear) is farmed, not fed, and
someone saving for an expansion spends later, not differently. Saving changes when a fragment is spent, not what it's
worth. (Kunark could change the rules; measure again then.) What the player knows and the app can't see is **how
high they take the gear they feed**. That sets a fragment's worth: the ratio in the row where their last fragment
lands, which for anyone feeding even one item to +10 is 4.5.

**Save VTPs for +9 → +10 (the user, 11-17).** A Void-Touched Potential adds one whole tier, whatever it costs, so it saves
the most on the costliest: +9 → +10 is 512 points, about 1,640 Majors through Infinite motes (or 364 Major fragments
condensed), against 256 points for +8 → +9. With every +9 → +10 done by a VTP, motes and fragments take gear to +9,
and a fragment's last landing place is +8 → +9: **3.56**. That's the recommendation, and the app's default.

The app asks exactly that (Settings, and the tray menu): +10 is 4.5, +9 3.56, +8 2.75, +7 2.29, and "+6 or lower, or
spells only" 2.0 (`Potential.FragmentValue`). It's the fragment value for T5-eq on Loot, Home, Ask and the drop
banner. Players who don't know the answer keep the default, **"Not sure: recommended"**
(`AppSettings.FragmentValueRecommended`). It follows `Potential.RecommendedFragmentValue`, +9's 3.56 (it was +10's 4.5
until the VTP recommendation, 11-17), so if the rules change (Kunark) the recommendation changes in one place and
everyone on it moves too. Since 11-17 the old 2.0 default migrates to it; a 1.0 or 1.6 picked by hand stays picked.
`UpgradePlanner` prices fragments at the recommended rate whatever the setting says. Upgrades says, under a plan that
spends a VTP below +10, that a VTP saves the most on +9 → +10. Community numbers count fragments at your value too:
the file keeps motes and fragments apart (from 11-17) and `CommunityView` adds them at the value Ask passes. Only an
older file, with them combined, counts a fragment at 2.0 (`CommunityBuilder.FragmentValue`).

Players read all this in Help → What's a fragment worth? (`index.html`, `#help-fragment`) and in the (i) popovers beside
Settings' question, Upgrades' VTPs and Ask's farm answers (`INFO` in `web/js/core.js`: `fragment`, `vtp`). They restate
the numbers above and the recommendation, so change them with it.

## How rare each tier is

Everything Venun looted to 11-17, mostly at D4: the two current logs, counted with a grep for `You looted` and
`You have looted` (units, not drop events).

| Tier | Motes | Fragments | Motes per 100 Majors |
|---|---|---|---|
| Major | 3,961 | 311 | 100 |
| Greater | 389 | 17 | 10 |
| Superior | 61 | 1 | 1.5 |
| Grand | 10 | 1 | 0.25 |
| Ascendant | 2 | 2 | 0.05 |
| Infinite | 0 | 1 | 0 |

Rarity doesn't change what a mote costs on a plan. Any mote above Major can be made from 2^(r−5) Majors, so that's
what spending one costs: a dropped Greater just saves condensing 2 Majors. Rarity would matter only for something
that can't be made. Fragments couldn't be made before the 11-17 patch, so they're charged at their best other use
instead. Since the patch, high fragments can be made from lower fragments, so a fragment's best use includes condensing it
first (next section). Dropped high tiers are part of the T5-eq/h farm rate already.

## What the logs and exports show

| Event | Recorded? |
|---|---|
| Mote or fragment looted (corpse or Reward Chest) | Yes |
| Motes condensed at Constructed Potential | Yes, with the count |
| Fragments condensed (since 11-17) | Yes, the same line with `Fragment`: `You offered 2 Fragment of Major Potential to Constructed Potential.` |
| Mote or fragment put into an item or spell | No. Only the refusal above is logged |
| Item merge | Yes, with the new +N |
| `/outputfile inventory` | Each item's +N is in its name (`Pyrosilk Robe +9`). No progress within the tier, and no motes or fragments, because currency storage isn't exported |

So an upgrade calculator needs the player to enter their mote and fragment counts once. After that it can follow
loot and condensing from the log, and only spending is unknown.

## Checked with the preview, 11-16

1. A Major mote into a +0 spell: Tier 4 at 1/16 (16 points), as the wiki says.
2. A Major mote into a +0 item: Tier 2 at 2/4 (5 points).
3. A Fragment of Major into an item at +5 or higher: refused. Fragments share the mote cap.
4. A Fragment of Major into an item with progress in its tier: still 8 points.

Still open: the spell max tier. A Fragment of Infinite on a +0 spell showed "Tier 10" with no fraction, so 10 may be the cap.

## Raw readings, 11-16 (one fragment each, previewed into a +0 spell and a +0 item)

Spell: Ascendant Tier 9 at 1/512, Grand Tier 8 at 1/256, Greater Tier 6 at 1/64, Infinite Tier 10, Major Tier 5
at 1/32, Lesser Tier 3 at 1/8, Potential Tier 4 at 1/16.

Item: Ascendant Tier 5 at 1/32, Grand Tier 4 at 7/16, Greater Tier 3 at 4/8, Infinite Tier 5 at 14/32, Major
Tier 3 at 1/8, Lesser Tier 2 at 1/4, Potential Tier 2 at 3/4, Superior Tier 4 at 1/16.
