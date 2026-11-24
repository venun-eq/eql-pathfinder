# Usage counts: the player chooses in the tour

**Why:** Anonymous usage counts are on by default today: the welcome tour's first page mentions them in one line, and
the switch is in Settings. The app goes on Reddit next week, to strangers, with the source public, and "telemetry on
by default" is the comment we expect. A new player makes the choice themselves on first run, with what's sent in
plain sight, and nothing is sent before they've chosen. Copies already installed keep the setting they have and
aren't asked again.

**Players:** every new player, first of all the Reddit stranger who reads privacy.html before installing. Worst for
one who closes setup at step 2: nothing of theirs may reach the server. Installed copies see no change.

Decided (the user, 11-24): neither answer is pre-selected and Next waits for one.

## What the player sees

**Today:** the Welcome page ends with "It does send anonymous usage counts (which pages you open, crashes) to help
improve it: see them or switch them off in Settings." Counting starts with setup and the first summary goes two
minutes after the app starts.

**After:** a step of its own, second in the tour, between Welcome and Your log. Nothing is counted for sending and
nothing is sent until the player answers it.

![The Usage step on first run, nothing chosen yet](usage-choice/choose.png)

![Yes chosen, and "See exactly what's sent" open](usage-choice/whats-sent.png)

Both pictures are drawn on the tour's own page in Edge (the app wasn't run): `usage-choice/mock.js` adds the step to
a copy of `welcome.html`. The summary in the second is a stand-in shaped like docs/usage.md's fields; the real one is
the app's own preview.

### First run, nothing chosen yet

- Progress bar: seven steps, the new one labelled **Usage**: Welcome · Usage · Your log · Reincarnations · Overlay ·
  Together · Ready. Each segment is as wide as its label needs (`flex: 1 1 auto`), so every label shows whole: with
  seven equal segments "Reincarnations" was cut to "Reincarnatio…" in the first draft of the mock-up.
- Eyebrow **USAGE COUNTS**. Heading **Send anonymous usage counts?**
- Lead: "Once a day, a short summary of how Pathfinder ran: pages opened, minutes, errors. It shows what to fix
  first. Nothing is sent unless you say yes."
- Two answers side by side, **neither chosen**, as radio cards (`role="radiogroup"`, label "Usage counts"):
  - **Yes, send them** / "Turn it off any time in Settings, and what was sent is deleted."
  - **No, don't send** / "Nothing is counted or sent. Turn it on any time in Settings."
- Below them a card with two lists:
  - **In the summary** (gold ▸): "Its own random id, not your sharing key" · "The date, Pathfinder and Windows
    versions" · "Pages opened, Ask questions, setup steps" · "Settings on: sharing, beta, overlays" · "Minutes run,
    with and without the game" · "Errors: where, never the message".
  - **Never in it** (grey ✕): "Your log, or any line from it" · "Character, server or guild names" · "Chat, your
    play's numbers, file paths" · "The time of day: only the date" · "Your IP address: not kept".
  - Under the second list, the link **See exactly what's sent**.
  - qa: check each line against what the recorder sends (docs/usage.md's table, the `state` words) before the
    tests are written. The lists, privacy.html and Settings' switch text say the same things.
- Footer: "Step 2 of 7 · Choose Yes or No to go on". **Next** is disabled, as on Your log before a character is
  picked. Back goes to Welcome. Enter does nothing while Next is disabled (the page's Enter handler already checks).
- Left picture: a small "Today's summary" card in the overlay's style: a few counts in gold, then "Character name",
  "Chat, log lines" and "Time of day" struck through with "never". It's a picture, like the overlay step's sample
  numbers.
- Everything fits without scrolling at the tour's fixed 1000 × 680 window (and so at 150%, where the window scales).

### An answer chosen

- The chosen card takes the selected look (gold border, filled radio), as Reincarnations' choice and the character
  list do. The footer goes back to "Step 2 of 7" and Next is enabled. The answer can be changed by clicking the other
  card, or later with Back, until the tour ends.
- The choice is saved at once, as the beta choice on Together is. **Yes:** counting goes on as today and the summary
  is sent on the usual schedule. **No:** nothing is counted, and what the setup counted before the answer is dropped
  unsent.
- No line appears to confirm it: the selected card is the confirmation (docs/design.md, "A line that only says all is
  well is noise").

### See exactly what's sent

- The link opens a box under the card: a small caption "Today's summary, as it would go now:" and the summary itself
  in the monospace box Settings uses for its preview (max 150 px tall, scrolls inside). The content scrolls down so
  the box is in view. The link reads **Hide what's sent** while it's open.
- It's the app's real preview, the same text Settings → Character and data → See exactly what's sent shows. Before
  an answer and with Yes, that's today's summary so far (on a first run: the start and the setup steps reached).
  With No, it's the app's own line: "Usage counts are off: nothing is kept or sent."
- While it's on its way, the box shows "Getting today's summary…" in grey, never an empty box.

### Closing the window without choosing

- First run: closing setup ends Pathfinder, as it does today on any step. Nothing is sent, the "setup closed before
  the end" count included. The next start opens setup at Welcome again and the Usage step asks again, nothing chosen.
- A player who answered and then closed setup: the answer stands. Next start, setup opens with it chosen; with Yes,
  the "setup closed" count goes with that start as today.
- No one gets past step 2 without answering, so there's no "finished setup but never chose" state.

### The tour from Help (every copy that has finished setup, including every copy installed before this)

- The same step, with the copy's current setting already chosen. Footer "Step 2 of 7", Next enabled.
- Clicking the other answer changes the setting at once, exactly as Settings' switch does. When usage counts are on,
  the **No** card's line reads "Stops the counts and deletes what this copy sent." instead, because that's what
  choosing it does there.

### Change character

- Unchanged: Your log · Reincarnations · Ready. No Usage step.

### Copies already installed

- Never shown the step on update; the setting stays as it is. The one-time Home banner (`usage-counts`) works as it
  does today for any copy that hasn't seen it. A new install never sees it: finishing the tour marks it seen, as now.

### The Welcome page

- Its privacy line loses its second sentence. It reads: "It only reads your log file. It never touches the game or
  its memory, and your play stays on this PC unless you choose to share it." The next page asks about usage counts,
  so the first doesn't need to.

### Settings

- Unchanged: the switch, its text and See exactly what's sent. A first-run No shows it off; a Yes shows it on.

### What's reused and what's new

All in `welcome.html`'s own styles (the tour doesn't load `app.css`):
- Reused: `.eyebrow`, `h1`, `.lead`; the two-answer grid `.choice` with `.opt` / `.opt.sel` (Reincarnations step);
  the round radio as `.char .radio` draws it; `.card`; `.sublbl`; `.link`; `.small`; Next's disabled look.
- New: the step `usage` (`data-step`, `data-art`); its radio cards (an 18 px radio column inside `.opt`); the
  two-column list card (`1.25fr 1fr`, ▸ and ✕ markers); the summary box (Settings' preview style, in the tour); the
  left picture; `.progress .seg { flex: 1 1 auto }`.
- The step key `usage` joins the tour's step names that are counted (`setup-usage`, `tour-usage`), and only when the
  answer is Yes.

### Words elsewhere that change with it

Change these in the same branch, so the app, the site and the docs say the same thing on launch day:

| Where | Today | After |
|---|---|---|
| `src/EqlPathfinder.App/web/welcome.html:265-266` | "…unless you choose to share it. It does send anonymous usage counts (which pages you open, crashes) to help improve it: see them or switch them off in Settings." | "…unless you choose to share it." |
| `site/privacy.html:17` | "Three things can: anonymous usage counts (on, with a switch), your play's numbers…" | "Three things can: anonymous usage counts if you say yes to them, your play's numbers…" |
| `site/privacy.html:24` | "On by default, so we can see how many copies are in use… Switch it off in **Settings → …**" | "Pathfinder asks when you set it up, and sends nothing until you say yes. They let us see how many copies are in use, which versions, which pages earn their place, and what crashes. Change your answer any time in **Settings → Character and data → Send anonymous usage counts**; switching it off stops the counting and deletes everything your copy sent from the server. Copies installed before <version> had them on by default and keep the setting they have." (rest of the paragraph as it is) |
| `site/privacy.html:14` | "Last updated November 23, 2026" | the day it ships |
| `site/index.html:113` | "Anonymous usage counts have a switch and a preview of exactly what's sent." | "Anonymous usage counts are sent only if you say yes, and you can see exactly what's sent." |
| `README.md:77` | "anonymous usage counts: on, with a switch that also deletes what your copy sent;" | "anonymous usage counts: only if you say yes when you set it up, with a switch that also deletes what your copy sent;" |
| `docs/usage.md:3-7` | "**On by default**, with a switch… The welcome tour's first page says it exists…" | "**Off until the player says yes**: the welcome tour's second step asks, with neither answer chosen (docs/specs/usage-choice.md). Copies installed before <version> keep the setting they had (on by default then) and the one-time banner." |
| `docs/usage.md:37-38` | step list "(welcome, log, reinc, overlay, goal, share, ready)"; `setupquit` | add `usage`; `setupquit` is sent only by a copy whose player said yes |
| `src/EqlPathfinder.App/AppSettings.cs:132`, `Usage.cs:9` | comments "On by default" | what's true after (a code comment, for the engineer) |
| Help | says nothing about usage counts (searched 11-24) | nothing to change |

Release note (players' text, for whoever writes `docs/releases/next/usage-choice.md`): "New copies ask whether to
send anonymous usage counts, with what's in them shown on the page, and send nothing until you say yes. If you
already have Pathfinder, your setting stays as it is: Settings → Character and data."

## Done when
- [x] First run, step 2 of 7: heading, lead, both answers unchosen, the two lists and the link, Next disabled with
  "Step 2 of 7 · Choose Yes or No to go on" - seen in `welcome-usage.png`
- [x] Every progress label shows whole, "Reincarnations" included - seen in `welcome-usage.png`
- [x] Yes chosen: its card selected, Next enabled, footer "Step 2 of 7" - seen in `welcome-usage-yes.png`
- [x] No chosen: its card selected, Next enabled - seen in `welcome-usage-no.png`
- [x] See exactly what's sent open, with the real summary in view - seen in `welcome-usage-sent.png`
- [x] Tour from Help with usage counts on: Yes chosen, No reads "Stops the counts and deletes what this copy sent." - seen in `welcome-usage-tour.png`
- [x] Welcome page without the usage sentence - seen in the Welcome step's shot
- [x] Before the answer, setup is counted in memory only: nothing is written to usage.json and nothing waits to be
  sent, and the step's preview still shows the steps so far - `Before_the_answer_setup_is_counted_in_memory_only_and_nothing_waits_to_be_sent`
- [x] Setup closed before the answer keeps nothing, the "setup closed" count included, and the next start finds no
  days - `Closing_setup_before_the_answer_keeps_nothing_not_even_the_setup_closed_count`
- [x] Yes keeps what setup counted, and from then on it's kept and sent as today - `Yes_keeps_what_setup_counted_and_from_then_on_it_is_kept_and_sent`
- [x] No drops what setup counted and counts nothing after - `No_drops_what_setup_counted_and_counts_nothing_after`
- [x] A settings.json from before this change (no answer in it) keeps its setting and is never asked - `A_copy_installed_before_the_question_keeps_its_setting_and_is_never_asked`
- [x] A new copy, or one closed before answering, is asked (again) whatever UsageEnabled says - `A_copy_that_has_not_answered_is_asked_whatever_the_switch_says`
- [x] An answer given stands on the next start - `An_answer_given_stands_on_the_next_start`
- [x] The window's wiring: a fresh isolated copy whose setup is closed at step 2 leaves no usage.json, no `UsageId`
  in settings.json, and sent no request to `/v1/usage` (POST or DELETE), a No on step 2 included - the engineer's
  isolated first run (`EQLPATHFINDER_DATA` and `EQLPATHFINDER_USAGE=1` against staging), its folder listed
  Run 11-24 (folders in the scratchpad: iso-quit, iso-no, iso-yes, iso2-yes; a local server logged requests): closed at step 2 left no settings.json, no usage.json; No left UsageChosen true, UsageEnabled false, no UsageId, no usage.json; Yes left usage.json with the setup steps and no UsageId yet (setup unfinished). The only request was GET /v1/config, never a POST or DELETE. The self-test did not reach Finish in any run (main's exe stalls at Ready the same way on a fake log), so "the id is made in Begin" rests on the code and engine tests.
- [x] The words in the table above are changed: `rg -n "On by default|on, with a switch|It does send anonymous" site README.md docs/usage.md src` prints nothing

Added 11-24 with "A delete that didn't land is asked again" (Decisions):
- [x] A No's delete is marked pending in the same save that switches off, and the id leaves `UsageId` - `A_No_moves_the_id_to_the_pending_deletes_in_the_same_save`
- [x] The server's answer clears the mark, "nothing to delete" included - `A_delete_the_server_answers_clears_its_mark`, and the Worker's `deleted: 0` test
- [x] A delete that fails is asked again, by the next start too - `A_delete_that_fails_keeps_its_mark_and_is_asked_again`, `A_pending_delete_is_asked_again_by_the_next_start`
- [x] Yes again while a delete is pending counts under a new id, and the old id's rows still go - `A_Yes_while_a_delete_is_pending_counts_under_a_new_id_and_the_old_one_is_still_deleted`, `A_second_No_before_the_first_delete_landed_keeps_both`
- [x] A copy switched off before this change has its delete asked once more; one that never had an id asks nothing - `A_copy_switched_off_before_this_has_its_delete_asked_once_more`, `A_copy_that_never_had_an_id_owes_no_delete`
- [x] A bad entry can't hold the others up - `An_id_the_server_would_refuse_is_dropped_without_a_request`
- [x] The window's wiring: an isolated copy (`EQLPATHFINDER_DATA`, `EQLPATHFINDER_USAGE=1`, `EQLPATHFINDER_API` at a
  local server that logs requests; never staging or production) with a settings.json that is on and has an id.
  With the server answering 500: after the No, settings.json has that id in `UsageForget`, no `UsageId`, and one
  DELETE was logged. Next start with the server answering 200: one DELETE with that id about 2 minutes in, and
  `UsageForget` is empty in the file afterwards. Its requests listed here.
  Run 11-24 (local logging server on 127.0.0.1, EQLPATHFINDER_API at it, a hand-written settings.json with a made-up id, never a copy): No with the server answering 500 left `UsageForget` [id], `UsageId` null, one `DELETE /v1/usage {"id":"0123…cdef"} -> 500`; the next start with 200 logged `GET /v1/config` 18:58:54 then `DELETE /v1/usage` with that id 19:00:51 (about 2 minutes) `-> 200`, and settings.json had `UsageForget` []. Nothing else was requested.
- [x] privacy.html says a delete that can't reach the server is asked again (the designer's words), docs/usage.md
  says the same and that a No then Yes is a new id, the release note has its line, and docs/debt.md's line on the
  failed DELETE is gone

## Edge cases
- **Nothing yet (first run):** the recorder starts held. `life.start`, `firstrun` and minutes come only after setup,
  so a Yes's first summary has the setup steps reached, then the start; a No's has nothing, ever.
- **Starting and stopping:** a crash on step 1 or 2 (`UnhandledException` saves usage) writes nothing; closing on
  step 2 sends nothing (`Dispose` sends only after `Begin`, and a held recorder has nothing pending anyway).
- **Re-sends and limits:** Yes then No on a first run sends no DELETE: nothing was ever sent, and no id exists
  before the first Yes. Yes, No, Yes again starts counting fresh; the steps before the No stay dropped.
- **Old versions in the field:** an older build reads this settings.json and ignores the answer field. So a new
  copy keeps `UsageEnabled` false until Yes: a downgrade before answering sends nothing.
- **A second copy:** development builds and isolated copies still count and never send. The `usage` self-test's
  copied settings.json has no answer in it, so it reads as chosen and runs as today.
- **A message that isn't the page data:** the step's choice and its preview are messages of the tour window
  (welcome.html's `feed`), never core.js's. The tour's "step" list takes `usage`.
- **Tour from Help, Change character:** Help's tour sets the switch as Settings does (DELETE on No as today);
  Change has no Usage step and never sees a held recorder (setup was answered before it can open).
- **Something leaving the PC (field by field, 11-24):** the step's lists are true, but leave out some of what's
  sent. "In the summary" has no line for features used and notices answered (`feature.*`), starts and updates
  (`life.*`), how long the log took to read and its size (`stats.import.seconds`, `import.mb`), the day this copy
  first counted (`firstSeen`), or `state`'s start window, goal and self-update. privacy.html leaves out `state`'s beta
  and self-update, and the log's size. Settings' switch text leaves out setup steps, features and settings. The
  designer words the fix, and the three say the same things on launch day.
- **Something stored that the step doesn't mention:** the server keeps `received`, the time the day's last
  summary arrived (server/src/usage.ts), to the second. "The time of day: only the date" is true of the summary,
  but not of the row (Not doing, below).

## Decisions

The architect, 11-24, before code, by the table in docs/engineering.md.

### What's stored: settings.json
- **The answer is `UsageChosen`, a `bool?` whose initializer is null: a missing value is an answer.** A file from
  before this change has no such field, reads null, and `UsageChoice.Read` keeps its switch. No Revision 4: an older
  build that opens the file writes `Revision` back to 3 and drops the field, so a migration would run a second time on
  the way back. Null reads the same before and after that round trip (Off and not asked, by the next line).
- **A new copy is `UsageChosen = false` and `UsageEnabled = false`, and the switch turns true only on Yes** (qa's
  recommendation). `UsageEnabled` stays the one value that says "may count and send", so an older build, Settings'
  switch and the banner are right without knowing the answer exists. Its initializer stays `true`: a file from before
  11-17 has no switch and is on today. The new copy's two values come from one place, which `Load` uses for no file,
  an unreadable file, and a file that reads as `null` (today `?? new AppSettings()`, which would be on and never
  asked).
- **`SetEnabled` is the answer, whoever calls it.** The step posts the tour's existing `setting` message with key
  `usage`; Settings and the banner call it as today. It sets `UsageChosen = true` when that was false, before its
  "already so" return: a first-run No finds the switch already off, and returning there would leave the copy
  unanswered and its recorder held.

### Before the answer
- **Counted in memory, as qa's tests have it,** and not "no recorder until Yes". The step's "See exactly what's sent"
  is then the real summary before the player decides, and a Yes keeps the first two steps and any error on them. A
  held recorder starts empty (it doesn't read a usage.json left behind); `Save`, `Pending` and `Dispose` write and
  offer nothing; `Choose(false)` empties it, deletes any file there and counts nothing after.
- **It fails closed: `Begin` on a recorder still held drops it as a No, without recording an answer.** No such path is
  known (no log folder is saved before step 3, so an unanswered copy always gets setup). If one appears, that copy
  sends nothing, Settings shows the switch off, and turning it on is the answer.

### What's sent
- **The id is made in `Begin`, and nowhere else** (today `Listen`; not on Yes either). Nothing is sent before `Begin`,
  so "settings.json has a `UsageId`" becomes the same fact as "this copy may have sent". `SetEnabled(false)` keeps its
  rule, DELETE when there's an id, and it's right on every path with no flag: No on a first run, Yes then No, and Yes,
  setup closed, No at the next start all send nothing; an installed copy that lands in setup again has its id and its
  No deletes. `firstSeen` is then the day setup finished. The step's preview has `"id": ""` during a first run: one
  line above it says the id is made when setup finishes (the designer reviews the words).
- **Nothing in the list stops being sent; all of it gets listed.** Each checked in the code: `state.start` and
  `state.goal` are one of a few setting words (Open/Minimized/Tray; balanced/speed/motes/rare) through
  `UsageKeys.Word`, `firstSeen` is a day, `feature.*` and `life.*` count the app's own events, and the log's read time
  and size are a count, sum and max to 0.1. The log's size is the one to word with care: it's a number about the
  player's file, so it's named under "In the summary", and "Your log, or any line from it" stays true beside it.
- **No new field and no change to the summary:** `SchemaVersion` stays 1, the Worker and the NAS job need nothing, the
  release note is `job: false`, `reread: false`. site/privacy.html changes here, with the step (the table above and
  the completed list).

### Who else reads it, and how updates arrive
- An older build on a new copy's folder reads the switch off and sends nothing; after a Yes it behaves as today. A
  copy updating from any version has no answer in its file and keeps its setting. Development and isolated copies
  count and never send, as now (`Sends`). A fresh isolated folder is a new copy and is asked; one with a copied
  settings.json isn't, so the `usage` self-test and the page tests run as they do.

### The window's rules (rule 4)
- **Two go to the engine, `UsageChoice.Step(start, hasId)`:** which answer shows chosen (none on Ask), and whether
  No's line is the deleting one (on, and the copy has an id: the same fact as the DELETE). The window sends the result
  in `init` and again after each choice, and the page selects what it's told, so the selected card is the setting
  that was saved. The engineer adds its theory to `UsageChoiceTests` before the code.
- **Next waiting for an answer stays in welcome.html,** beside Your log's same rule, and the shots prove it.

### Limits, cost, taking it back
- Fewer copies send, so nothing new to limit. From this version the counts are of copies that said yes: installs and
  the setup funnel in docs/usage.md become a floor, and that page says so in this branch.
- A later build can ask again or stop asking: the field only adds. Turning counts on for a copy that never said yes is
  the one change that would be the user's to make.

### The server's `received`: usage rows keep the day only
Decided by the user, 11-24 ("yes, store only the day", relayed by the lead). How:

- **In a branch of its own, not this one.** The app ships itself within the hour of `finish`; the server goes by hand,
  staging first. Nothing this branch says depends on it (the step and privacy.html speak of what's sent), so either
  lands first, and this branch's `finish` doesn't wait on the Worker's tests. This section is that branch's decisions:
  copy it into its spec.
- **The column goes: `server/migrations/0010_usage_day_only.sql` is `ALTER TABLE app_usage DROP COLUMN received;`**
  and `usage.ts`'s INSERT and its ON CONFLICT stop naming it. A row is then id, day, app, channel, body, and the only
  date is its own `day`. The arrival day isn't kept either: nothing reads it (the cron prunes by `day`, no query in
  docs/usage.md uses it), and beside the player's local `day` it narrows the hour. Dropped and not zeroed, because a
  table with no time column can't store one whatever the Worker does, and the public schema shows it.
- **Why it matters beyond the wording:** `units.received` and `feedback.received` are to the second too, and a copy
  sends its summary and its shares in the same session. The arrival second is the one thing that could line a usage
  id up with a sharing key, which docs/usage.md and privacy.html say can't be done.
- **Rows already stored** keep everything but the time: the same migration removes it from all of them. D1's Time
  Travel can still restore the old values for up to 30 days, then they're gone; nothing brings them back after that,
  which is the point.
- **Order: migration, then the Worker, back to back** (the usual order). In between, the old Worker's INSERT names a
  column that's gone and answers 500: the app keeps the day (`UsageClient` throws, the day stays pending up to 14
  days) and sends it at the next round. It fails closed: in that gap nothing can store a time. Worker first would
  fail the other way (`received` is NOT NULL with no default) and leave timed rows.
- **The Worker's tests must prove:** (1) after `./migrations`, `pragma_table_info('app_usage')` is exactly id, day,
  app, channel, body, so a time column coming back fails; (2) "keeps only the known fields" also sends `received`
  and a clock time and finds neither in the row; (3) the send, re-send and delete tests pass on the new table, which
  they can't if the INSERT still names the column; (4) the cleanup test's INSERT loses `received`. If workerd's D1
  refuses DROP COLUMN, the test setup fails at once: rebuild the table in the same migration instead.
- **What the tests can't prove,** because the harness applies every migration before any row exists: that stored
  rows survive. Staging proves it: `SELECT COUNT(*)` and one row's `body` before and after.
- **By hand, after the user says so:** staging: count, `npm run migrate`, `npm run deploy`, count and
  `PRAGMA table_info(app_usage)`, then the `usage` self-test against staging (sends, the row is there, the switch
  deletes it). Production the same, the two commands back to back, before the launch post.
- **Words, in that branch:** docs/usage.md's Server paragraph (the columns), and one sentence in privacy.html that
  the time a summary arrives isn't kept (the designer's words), deployed after production is migrated.

### A delete that didn't land is asked again
The architect, 11-24, after the second review's debt line (a DELETE that fails is never tried again, while
privacy.html says off "deletes everything your copy sent"). The user: "yes I agree with a delete pending setting".
Built in this branch, before it reaches main.

**What's stored: settings.json**
- **`UsageForget`, a `string[]` whose initializer is `[]`: the ids whose rows the server must still delete, oldest
  first** (`IUsageSettings`, `AppSettings`). A list and not a flag because of "Yes again" below. An array, replaced
  whole and never changed in place, because the forget reads it off the UI thread.
- **A No retires the id.** `SetEnabled(false)` on a copy with an id moves it from `UsageId` to `UsageForget` and
  clears `UsageSince`, in the one `Save` that writes `UsageEnabled = false`. Set before the DELETE goes out and not on
  failure: the app closing or crashing mid-request is one of the failures, and it can't record what it doesn't live
  to see. `UsageSwitched.ForgetId` is still that id (non-null = ask now).
- **So `UsageId` means "the id this copy counts under now", and an Off copy has none. A later Yes gets a new id in
  `Begin`,** which is still the only place one is made. This replaces today's reuse. A kept id would make the pending
  DELETE and the new POSTs fight over the same rows (the delete landing late removes days the recorder believes are
  sent), and the only cure is holding every send until the delete lands. With a new id the two share nothing. It's
  also what "deleted" should mean: the server can't tell the copy that came back from a new one.
- **A file without the field has nothing pending** (the initializer), and the Engine reads a `null` as empty. No
  Revision 4, for the reason `UsageChosen` has none. An older build drops the field when it saves, so a downgrade
  loses a pending delete, as every failed delete is lost today.
- **An installed copy that is Off and still has a `UsageId`** switched off under an older build, whose DELETE may
  never have landed. `Listen` moves that id to `UsageForget` and saves, so it's asked once more and "Off has no id"
  is true of every copy. One request per such copy, once. (The user's to confirm: the report.)

**When the mark clears**
- **On any 2xx to DELETE `/v1/usage`, `deleted: 0` included:** nothing to delete is deleted. Everything else keeps
  it: no answer, a timeout, 429, 5xx and 4xx. `UsageClient.ForgetAsync` stays as it is (`EnsureSuccessStatusCode`).
- **Cleared by `UsageSwitch.Forgotten(id)` on the UI thread,** which removes the id and saves. Every settings write
  is on that thread today (Sharing.cs's `Ui`): `AppSettings.Save` has no lock and serializes the whole object. The
  forget itself runs off it (the second review's fix), so `ForgetAsync(delete, forgotten)` is handed the clearing as
  it is handed the DELETE, and `UsageService` passes `id => Dispatcher.BeginInvoke(() => _switch.Forgotten(id))`.
  A delete that lands as the app closes may not get its clear: the next start asks again, the server answers
  `deleted: 0`, and it clears then.
- **An entry that isn't 32 lowercase hex (the server's `HEX32`) is dropped without a request.** Only a hand-edited
  file has one; the server would answer 400 every hour for good and hold up the ids behind it.

**When it's asked again**
- **At the No, at once (as today); 2.5 minutes after every start (30 s after the send timer); every hour while the app runs.** Off copies too: a
  player who said No offline and leaves Pathfinder in the tray for a week shouldn't wait for a restart. One timer
  in `UsageService`, started in `Begin` when `Sends`, due `FirstSend` + 30 s and then `Every`, whose tick returns at once
  when nothing is pending. `Stop()` leaves it alone (a No calls `Stop`, and that's when it's needed); `Dispose` ends
  it. A setup closed before `Begin` asks nothing that run.
- **One attempt is `UsageSwitch.ForgetAsync`: under the round's gate, oldest first, one DELETE an id, stopping at
  the first that fails** (offline fails them all, and each failure can cost the 20 s timeout).
- **No end date and no cap on the list.** Every id in it may have rows, so dropping one breaks the promise. The cost
  is one small request an hour per pending copy, the same as a copy that's on and offline. The server prunes at 400
  days anyway; ending the retries then needs a date per id, which isn't stored until there's a reason.
- **A copy that doesn't send (`Sends` false: a development build, an isolated copy) marks and neither asks nor
  clears.** A development build on the real folder leaves the mark for the released build's next start.
- Closing is unchanged: a delete in flight gets up to 3 s, and nothing is asked just because the app is closing.

**Yes again while a delete is pending**
- **New counts go under the new id at once and wait for nothing; the old id stays in `UsageForget` until its
  DELETE lands,** before or after the new POSTs. The gate still does what the second review needed: a POST on the
  wire under the old id finishes before that id's DELETE.
- **No again before the first delete landed: both ids are listed** and deleted in order. An id that never posted
  is deleted too: a POST whose answer was lost may have landed, so "has an id" stays the test.

**A copy that never had an id** (a first-run No; Yes then No before setup ends): nothing is listed, nothing is
asked, as now.

**What's sent**
- **Nothing new: the same DELETE `/v1/usage` with `{"id": …}`, possibly more than once.** What's different is who
  can send it: a copy that's Off, at its start, and a copy that's on again under another id. The server sees a
  retired id once more and keeps no address. The Worker doesn't change; one Worker test pins what the retry rests
  on, that an id with no rows answers 200 `{ ok: true, deleted: 0 }`. No deploy. `job: false`, `reread: false`.
- **The numbers:** a copy that says No and later Yes is a new id with a new `firstSeen` and a `life.seen`, so
  distinct ids count it twice across the gap. docs/usage.md says so.

**Words** (the least that keeps the pages true)
- site/privacy.html, after "…deletes everything your copy sent from the server.": one sentence, the designer's
  words, such as "If the server can't be reached then, Pathfinder asks again until it's done."
- The tour's cards and Settings' switch text stay: "what was sent is deleted" is true. No "delete pending" line in
  Settings: it resolves by itself, and the player has nothing to do about it.
- docs/usage.md: the id's row (a new one after a No), "Switching it off" (the mark, when it's asked again, what
  clears it), and the note on distinct ids. The release note gets one line for players.
- docs/debt.md's line on the failed DELETE goes when this is built.

**Limits, taking it back**
- Ten times the players changes nothing: a request goes only while a mark is pending. A server outage costs one
  DELETE an hour from each copy with a mark. The route's rate limit answers 429, which keeps the mark.
- The field only adds, and a later build can ignore it. Deleted rows don't come back, which is the point.

**Tests the engineer writes first** (`UsageChoiceTests`; the fake settings gain `UsageForget` and remember what
each `Save` wrote; the helper's id maker returns the same id every time, so these need one that doesn't):
1. `A_No_moves_the_id_to_the_pending_deletes_in_the_same_save`: on, begun, id A. After `SetEnabled(false)` the one
   save holds `UsageEnabled` false, no `UsageId`, `UsageForget` [A]. Fails if the mark is set on failure or later.
2. `A_delete_the_server_answers_clears_its_mark`: `ForgetAsync` with a delete that returns: `UsageForget` empty, saved.
3. `A_delete_that_fails_keeps_its_mark_and_is_asked_again`: the delete throws `HttpRequestException`: it passes
   through, [A] stays; a second `ForgetAsync` asks for A again and clears it.
4. `A_pending_delete_is_asked_again_by_the_next_start`: a new `UsageSwitch` on settings that are Off with
   `UsageForget` [A] (and once with `null`: nothing pending, no throw): after `Listen` and `Begin`, `ForgetAsync`
   asks for A. Proves the file alone carries it.
5. `A_Yes_while_a_delete_is_pending_counts_under_a_new_id_and_the_old_one_is_still_deleted`: A pending, Yes: `UsageId`
   is B, a round's payloads carry B, `ForgetAsync` asks for A and never B.
6. `A_second_No_before_the_first_delete_landed_keeps_both`: [A, B], asked in that order; when B's fails, A is
   cleared and B stays.
7. `A_copy_switched_off_before_this_has_its_delete_asked_once_more`: Off with `UsageId` A and no field: after
   `Listen`, no `UsageId`, `UsageForget` [A], saved, no recorder.
8. `A_copy_that_never_had_an_id_owes_no_delete`: a first-run No, and Yes then No before `Begin`: nothing listed,
   `ForgetAsync` asks nothing.
9. `An_id_the_server_would_refuse_is_dropped_without_a_request`: ["nothex", A]: only A is asked, both go.
10. The held tests keep their meaning with the new signature: `The_delete_starts_only_after_the_held_post_returns`
    and `A_forget_waiting_behind_a_round_resumes_off_the_caller_thread` get their id into the list by the No.
11. `server/test/usage-feedback.test.ts`: a DELETE for an id with no rows answers 200 `{ ok: true, deleted: 0 }`.

What the tests can't reach is `UsageService`'s timer, `Sends` and the dispatcher's clear: the isolated run in
"Done when" proves those, against a local server only.

**Where:** `Engine/Usage/UsageSwitch.cs` (`IUsageSettings`, `Listen`, `SetEnabled`, `ForgetAsync`, `Forgotten`);
`App/AppSettings.cs` (the field); `App/Usage.cs` (`SetEnabled`, `ForgetAsync`, the timer in `Begin`, `Dispose`);
the words above.

## Not doing
- Asking copies already installed, or changing their setting.
- Dropping or rounding the server's `received` time: that's the server, by hand. Recommended before the launch post:
  store the day only, since nothing reads it (the user's call).
- Pre-selecting an answer (decided, above). If it changes: welcome.html's initial selection and Next's rule, and
  passing step 2 posts the pre-selected answer. The engine and these tests stay as they are.

## Where
- `src/EqlPathfinder.Engine/Usage/UsageRecorder.cs`: `held` and `Choose` (stubs now). `Usage/UsageChoice.cs`: `Read` (stub now).
- `src/EqlPathfinder.App/AppSettings.cs`: the answer field (absent = chosen), a new copy's values in `Load`.
- `src/EqlPathfinder.App/Usage.cs`: `Listen` held when asked, the id made on Yes, `SetupQuit`, `SetEnabled`.
- `src/EqlPathfinder.App/App.xaml.cs` `OnStartup`; `WelcomeWindow.xaml.cs`: `SendInit`'s usage state, the choice
  and preview messages, the "step" keys; `web/welcome.html`: `STEPS`, the step, Next's rule, the Welcome line.
- `src/EqlPathfinder.App/DevSnapshots.cs`: the welcome shots the Done-when lines name.
- The words: site/privacy.html, site/index.html, README.md, docs/usage.md, `docs/releases/next/usage-choice.md`.

## Reviews
- architect: (11-24, review of 1b2c85a) one must fix: the id made in `Begin` only, a held recorder at `Begin` dropped as a No, and `SetEnabled`'s answer rules (chosen before the "already so" return, a held No dropping, a held Yes keeping, a DELETE only with an id) are in `UsageService` with no test, and the isolated runs never reached `Begin`. Move them into the Engine (an `IUsageSettings` that `AppSettings` implements, as it does `IPlannerSettings`, and a `UsageSwitch` owning the recorder's `Listen`/`Begin`/`SetEnabled`, with the id maker passed in and the forget id returned; `UsageService` keeps the timers, HTTP, `State` and `Sends`) and test them in `UsageChoiceTests`. Should fix: `Closing_setup_before_the_answer_keeps_nothing…`'s next start is a held recorder, which never reads the file, so `Assert.Empty(next.Days)` can't fail: open the next start unheld. Sound: `UsageChosen` null in an old file reads as answered, `NewCopy` for no file, an unreadable one and a null; a first-run No, Yes then No, and Yes, No, Yes send no DELETE and make no id; nothing written or pending while held; the privacy page and the step list every field of `UsagePayload`; `git merge main` merges cleanly. The staging row after the usage self-test is the self-test's own doing (it turns counts on again and Quit sends under the kept id); a real in-flight send can also land after the DELETE, from before this branch: docs/debt.md.
  - Fixed 11-24: the rules moved to the Engine: `IUsageSettings` and `UsageSwitch` (Engine/Usage/UsageSwitch.cs; `AppSettings` implements the interface); `UsageService` keeps timers, HTTP, `State` and `Sends`. Tests in `UsageChoiceTests`: `The_id_is_made_in_Begin_only`, `A_recorder_still_held_at_Begin_is_dropped_as_a_No_and_no_id_is_made`, `SetEnabled_marks_the_answer_chosen_before_its_already_so_return`, `A_No_while_held_drops_the_counts_and_asks_for_no_delete`, `A_Yes_while_held_keeps_the_counts`, `A_delete_is_asked_for_only_when_there_is_an_id`. `Closing_setup_before_the_answer_keeps_nothing…` opens its next start unheld. The in-flight send is fixed here: `ForgetAsync` takes `_gate`, and `SendCoreAsync` asks `UsageSwitch.MayPost(recorder)` before its work and before each POST (test `A_send_round_that_started_before_a_No_may_not_post_after_it`). docs/debt.md keeps only the self-test half (it still turns counts back on).
  - Second review 11-24 (17c49a5..ba5f5c1): one must fix: nothing proves the in-flight close. `A_send_round_that_started_before_a_No_may_not_post_after_it` tests `MayPost` alone, and removing `ForgetAsync`'s `_gate` or the per-POST `MayPost` in `SendCoreAsync` (App/Usage.cs) fails no test. Move the round's loop and the forget's ordering into the Engine with the POST and DELETE passed in, and test with a POST held open: a No mid-round gets no second POST, and the DELETE starts only after the held POST returns. Should fix: `ForgetAsync` resumes on the UI thread after `_gate`, so a switch-off then quit within a send's 20 s loses the DELETE (`Dispose` disposes `_http`, and the `ObjectDisposedException` that follows escapes its catch): `ConfigureAwait(false)` there, and `Dispose` waits up to 3 s for a pending forget before `_http.Dispose()`. Sound: the move keeps every behaviour the first review found sound (new copy, No, Yes then No, installed copies, nothing written while held; `Stop` after the clear is harmless); each of the six new tests fails when its rule breaks, and the closing test can now fail; a POST already on the wire when the No arrives finishes before the DELETE (the waiter is queued synchronously and `WaitAsync(0)` can't jump it, so a quick Yes's round comes after the DELETE); `Dispose`'s send takes the gate with `WaitAsync(0)` and the UI never blocks on it, so no deadlock or hang. A DELETE that fails (offline, or closing) is never retried: docs/debt.md.
    - Fixed 11-24: the round's loop and the forget's ordering moved into the Engine (`UsageSwitch.SendRoundAsync` / `ForgetAsync`, one gate, the POST and DELETE passed in); `UsageService` keeps the timers, HTTP, `State` and `Sends`. Tests with a POST held open: `A_No_in_the_middle_of_a_round_gets_no_second_post`, `The_delete_starts_only_after_the_held_post_returns`, `A_forget_waiting_behind_a_round_resumes_off_the_caller_thread`. Seen failing: without the forget's gate the second and third fail; without the per-POST `MayPost` the first and second fail; without `ConfigureAwait(false)` the third fails. The forget also catches `ObjectDisposedException`, and `Dispose` waits up to 3 s for a pending forget before `_http.Dispose()` (the wait itself is App-only, not tested).
  - Third review 11-24 (d746989..5f1e644): no must fix. Should fix: `UsageSwitch.IsId` (UsageSwitch.cs:193) throws on a `null` entry (a hand-edited `[null]`), every hour, before the ids behind it: `id is { Length: 32 } && …`, and a null in `An_id_the_server_would_refuse…`'s list. `UsageService.Begin` (Usage.cs:74) starts the forget timer on the send timer's `FirstSend`/`Every`, so a copy on again with an old id pending can have its round answer "busy" and skip that hour: start the forget 30 s later. Sound, by reading: every decision of be8c95b is met (the mark in the switch's one save, `UsageId` null when Off, a new id only in `Begin`, any 2xx clears, non-hex dropped unasked, start + hourly for Off copies when `Sends`, nothing at close); tests 1-9 and the Worker's each fail when their rule breaks (mark after the save, clear on failure, a kept id on Yes, both ids and their order, the pre-field Off copy, an id made before `Begin`, a non-hex asked); a quit between the save and the DELETE, or between the 2xx and the clear, is asked again at the next start; `Retire` and `Forgotten` both run on the UI thread and replace the array whole, so no lost id and no `Save` off it (a stale read off-thread can only repeat the same DELETE); the Off copy's leftover id is retired in `Listen` once, since an Off copy never gets an id again; a first-run or held copy has no id to list; the only request is the same DELETE; ticked boxes 170-182 match the code, the run and the words.
  - Fixed 11-24: `IsId` takes `string?` (`id is { Length: 32 } && ...`) and the test's list gains a null; the forget timer starts 30 s after the send timer (2.5 minutes after start).
- designer: (11-24, the engineer's shots and a fresh render of a135c7f) one must fix: Settings' switch text (settings.js) leaves out notices answered, starts and updates, and the day this copy first counted, so the step, privacy.html and Settings don't list the same things. Should fix: with "See exactly what's sent" open, the cards' last line is sliced right under the progress labels (fade `.content`'s top ~14px); the id note says "only if you said yes" before any answer ("…only if you say yes"); "Errors: where, never the message" → "Errors: their kind and where, never the message", as privacy.html and Settings say; privacy.html's "They let us see" has no subject ("Usage counts let us see"); "your play's numbers" under Never beside "Minutes run, with and without the game" reads as a contradiction to a stranger ("Chat, your kills, levels or loot, file paths"). Sound: a fair choice (parallel cards, neither chosen, the same selected look), seven labels whole, the step fits the fixed 1000 × 680 window with no scrollbar at HEAD (the engineer's shots predate dropping "in MB" and showed one), footer and Next in each state, tour from Help's No line, the Welcome line, index.html, README and the release note.
  - Fixed 11-24: Settings' switch text (settings.js:62) lists the same things as the step and privacy.html, in the designer's words; the id note reads "empty for now ... only if you say yes", the errors line "their kind and where, never the message", Never says "Chat, your kills, levels or loot, file paths" (welcome.html); privacy.html says "Usage counts let us see"; with the summary open `.content`'s top 14px fades (`.content:has(#usageSent:not([hidden]))`). Seen in scratchpad snap2: welcome-usage, -yes, -no, -sent and main-settings-usage (a new shot in DevSnapshots.cs).
