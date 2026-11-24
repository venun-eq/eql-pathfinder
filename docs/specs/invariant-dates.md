# Times and days written for the machine are the same on every PC

**Draft, 11-19** ([code-hygiene](code-hygiene.md)). **Tier:** risky, 600k: it changes what's stored and what's sent.
**Waits for:** nothing. Not alongside cs-format, since both change `PathfinderStore.cs` and `FightStore.cs`.

**Why:** a player whose Windows is set to Finnish or Indonesian gets times written as `2026-11-19T14.03.22` in the
app's store, which it then can't read back. What they see from that (an error, an empty History, a crash) isn't
known yet: qa finds out. A player on a Thai PC shares a day of 2569-11-19, and one on a Persian or Saudi PC a day in
that calendar.
**Players:** anyone whose Windows regional format isn't English-like. Everyone else sees no change.

## What was found (11-19)

CA1305 found these during the [code-hygiene](code-hygiene.md) review. Each formats a date with a custom format and
the PC's culture. In a custom format, `:` is the culture's time separator, and `yyyy` counts in the culture's calendar.

| Where | What | What it does |
|---|---|---|
| `Storage/PathfinderStore.cs:354` `Iso` | `ToString("yyyy-MM-ddTHH:mm:ss")` | Writes the loot, kill, level and bank times. It's read back with `DateTime.Parse` in the PC's culture (lines 183, 210, 225, 250, 265, 277, 301) |
| `Combat/FightStore.cs:195` `Iso` | the same | Writes fight times. They're read back with the invariant culture (lines 142, 147, 148), so they fail on any PC that writes a dot |
| `Sharing/SharePayload.cs:207` `Day` | `ToString("yyyy-MM-dd")` | The day sent with a share |
| `Tracking/Tracker.cs:150` | `ToString("yyyyMMddHHmmss")` | A visit's id |
| `Pages/AskPage.cs:149, 241` | `ToString("yyyy-MM-dd")` | Dates handed to the page, which may be compared or parsed there |

What was checked, with Windows PowerShell's .NET Framework locale data:
- fi-FI and id-ID write `14.03.22`. Parsing that fails in their own culture and in the invariant one.
- th-TH writes `2569-11-19`, fa-IR `1405-08-28` and ar-SA `1448-06-09`.
- en-US, de-DE and da-DK write colons and Gregorian years.

The app runs on .NET 10 with ICU locale data, so qa reproduces it there first.

## Proposal

- Every date or time written for the machine (stored, sent, an id, handed to a page as data) uses the invariant
  culture, or the "s" and "o" formats, which ignore the culture.
- Reads accept what such PCs already wrote: the dot form, and dates in the culture's calendar if any reached the
  store. The architect decides between rewriting those rows once and reading both forms for good.
- What the server holds: qa checks whether shares from such PCs are already in the pool with wrong days, and the
  architect decides what happens to them. Clock times are never sent (CLAUDE.md), so the store's times stay on the PC.
- A sweep of the rest: every `ToString`, `Parse` or `Convert` that reaches storage, the server or a page as data.
  cs-analyzers' baseline lists them.

## Done when

(qa writes this, with tests that run the store, the fight store and the share under fi-FI and th-TH.)

## Edge cases

- A player who changed regional settings between sessions has rows in both forms.
- Visit ids already written in another calendar: whether anything compares or sorts them.
- A release with `reread: true` if fixing the ids or times means reading the logs again (the architect decides).

## Where

- `src/EqlPathfinder.Engine/Storage/PathfinderStore.cs`, `Combat/FightStore.cs`, `Sharing/SharePayload.cs`,
  `Tracking/Tracker.cs`, `Pages/AskPage.cs`
- `server/src/`, if the Worker should refuse a share whose day is years off
- If cs-analyzers lands first, this branch takes these rows out of its baseline
