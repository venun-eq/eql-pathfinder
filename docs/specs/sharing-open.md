# Sharing is open to everyone

**Why:** On 11-19 the user opened sharing: no invite code needed, at most one new player per address a day. The app
already follows the server (it stops asking for a code), but the website's home page and privacy page still said
"invite-only for now", and the home page told players to get a code from a guildmate.
**Players:** anyone reading the website before they install or turn sharing on.

## Picture

```text
Before (site/index.html, Sharing):  ... Never names, chat or clock times. Sharing is off until you turn it on, and
                                    invite-only for now: a guildmate who shares can give you a code from Settings.
After:                              ... Never names, chat or clock times. Sharing is off until you turn it on in
                                    Settings.

Before (site/privacy.html):  Off until you turn it on in Settings → Sharing, and invite-only for now. When it's on, ...
After:                       Off until you turn it on in Settings → Sharing. When it's on, ...
```

## Done when
- [x] Neither page mentions invite-only or a code: `grep -ci invite site/index.html site/privacy.html` prints 0 for both
- [x] The site still builds: `tools/check.ps1` (it builds the site when `site/` changed)
- [x] Registration is open in production and the brakes work: `pathfinder share-smoke --rules` against
  `https://api.eqlpathfinder.com` on 11-19 printed "enrollment open", registered without a code, and answered 429
  `too_many_players` for a second new player from the same address

## Not doing
- `min_player_days` = 3 today: it would hide 3 of the 5 sharers for up to a day. It goes on 11-20 evening
  (docs/community-job.md, "Opened 11-19").
- `min_players` = 3: with 5 sharers it would hide most of Ask.

## Where
- `site/index.html`: one sentence
- `site/privacy.html`: the Sharing sentence, and "Your internet address" names the new-player limit (Decisions)
- `docs/community-job.md`: what's on in production and what's left

## Decisions
- **Order of the switches:** the brake before the door. `max_new_players_per_ip` = 1 went in before
  `require_enrollment` = 0, so there was never a moment with registration open and no cap. Both are config rows the
  Worker reads per request: no deploy, no release, and the app follows `requireEnrollment` from `GET /v1/config`.
- **`min_player_days` = 3 waits until 2026-11-21 01:30 UTC (11-20 8:30pm New York).** The job counts a player once
  `players.created <= now - 3 × 86400` (CommunityJob.cs), and the newest of the five registered 2026-11-18 01:22 UTC,
  so from then on none of them is hidden. The rule reads `players.created`, not when the row was set, so it applies
  backwards: a player made today is still held until its third day. Waiting only lets such a player count for about
  a day, behind the one-per-address cap; worth it to keep Ask filled for the five who share.
- **Taking it back:** `require_enrollment` = 1 closes registration again on the next request. Players registered
  meanwhile stay; that's the only part that isn't undone, and the user accepted it with the go.
- **What's sent:** nothing new leaves the PC. **What's kept:** since the cap went on, the server writes
  `register_ips` (a code from the address, the day and `IP_SECRET`, deleted after 2 days); before 11-19 it never did,
  because `newPlayerLimit` runs only with the cap above 0. privacy.html's "Your internet address" names only the
  one-character use of that code, so it must name this one too, in this branch. Its paragraph's third sentence becomes:
  "For sharing, it keeps a code made from your address, the day and a secret key, to allow one new sharing player a
  day per connection and one character per game server per connection (the game allows one character per server)."
  Nothing more: it needn't mention the 429, `min_player_days` (it reads only the server's own registration date) or
  `min_players` (what Ask shows, not what's kept).

## Reviews
- architect: one must fix, the privacy sentence under Decisions ("Your internet address" now has two uses of the
  address code); the switch order, the 11-21 01:30 UTC timing and the two removed clauses are sound, and no other page
  still says invite-only.
- designer: checked the three sentences against design.md (plain words, players' terms, short sentences). The two
  removed clauses leave clean sentences. The privacy sentence was 45 words and used "sharing player", a term found
  nowhere else on the site: split in two, the second now "With it, only one new player a day can start sharing from
  the same connection, and sharing stays at one character per game server per connection (...)". Every fact the
  architect required stays: the code, its three parts, both uses, per connection.
