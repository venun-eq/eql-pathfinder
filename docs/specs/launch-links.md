# GitHub, Discord and website links, and the Windows warning

**Why:** Pathfinder is advertised on Reddit this week, to strangers for the first time (9 copies today, all friends),
and its source goes public on GitHub under MIT at the same time. Today nothing links to GitHub or Discord: not the
site, not the app, not README.md. And a stranger who meets the blue "Windows protected your PC" box with no word
from us will most likely close it and downvote: the user, "without the code being public, when people meet a Windows
Security Defender warning, they will probably just not install it, and downvote it."
**Players:** a stranger from Reddit deciding whether to install (the site, README); every player once installed
(Help, Settings, the tray, the welcome tour).

## What the player sees

Every link goes to `https://eqlpathfinder.com/github` or `https://eqlpathfinder.com/discord`, never to github.com or
discord.gg directly, so a copy installed today never holds a dead link. The app's windows can only open
eqlpathfinder.com addresses anyway (`WebViewPolicy.IsAllowedExternalUrl`), so the redirects are also what lets the
app open them. The words are "GitHub" and "Discord", their own names; icons are their usual marks, drawn in the text
colour, never in brand colours.

### The website: every page

**Before:** the header has What's new and Privacy; the footer has Home, What's new, Privacy and the Daybreak line.

**After** (both partials, so index, changelog, privacy and 404 all get them):
- Header: `What's new` `Privacy` | `[GitHub mark] GitHub` `[Discord mark] Discord`. A thin divider (`--line`)
  separates the site's pages from the two places off the site. Below 560 px wide the two show their marks alone
  (with `aria-label="GitHub"`, `"Discord"`), so the header stays on one line.
- Footer nav: `Home` `What's new` `Privacy` `GitHub` `Discord`. A new first line under it, in `--soft`:
  **Made by Venun. Free and open source under the MIT license.** Then the Daybreak line as it is.

### The website: the home page

![The hero with the Windows note](launch-links/site-hero.png)

- The meta line under the buttons: **Free · Open source · Windows 10 and 11 · Signed installer, updates itself**.
- Under it, a quiet note in a panel (`--panel`, `--line` border, 15 px, a gold shield-with-! mark): **First install?
  Windows may say Windows protected your PC. Click More info, then Run anyway.** with a link **Why it shows** to
  `#windows-warning`. The box's own words (Windows protected your PC, More info, Run anyway) are in `--text`, bold.
  It is a note, not a warning: no amber, no red (rule 21: a colour is a verdict's).
- The mock-up crops the Home screenshot only to fit the picture; the screenshot stays as it is.

![Trust, the Windows section and the final download](launch-links/site-windows-warning.png)

- **Trust** gets a fourth card, and the third is reworded:
  - **Signed and current:** "The installer and the app are signed, and Windows shows a publisher Microsoft has
    verified. New versions arrive by themselves."
  - **Open source** (new): "All of Pathfinder's code is on GitHub under the MIT license. Read what it reads and
    sends, or build it yourself." "GitHub" is the link.
  - Four cards sit in one row on a wide screen; the grid's `auto-fit` already stacks them when narrow.
- **A new section after Get started**, `id="windows-warning"`, laid out as a `.feature` (text left, picture right):
  - Kicker **Installing**. Heading **Windows may warn you the first time.**
  - "Windows shows this blue box for a new app until enough people have installed it. It's about how new
    Pathfinder is, not about what's in it."
  - Three numbered steps (gold discs, as the picture's markers):
    1. Click **More info**.
    2. Check the publisher: a name Microsoft has verified, not **Unknown publisher**. It's the developer's own name;
       Pathfinder is published as Venun.
    3. Click **Run anyway**.
  - "Rather look first? Pathfinder's source is public on GitHub: what it reads, what it sends, and how to build it
    yourself."
  - The picture: the blue box drawn twice in HTML and CSS (no image of a real dialog), before and after More info,
    with gold rings and the numbers 1, 2, 3 on More info, the Publisher line and Run anyway. The second box reads
    "App: EQLPathfinder-win-Setup.exe" and "Publisher: *the developer's name*", in italics: **the site never prints
    the certificate's name.** One `role="img"` with an `aria-label` that says what the two boxes show. The site's
    CSP allows no inline styles, so the markers' places are classes.
  - Below 900 px the picture goes under the text, as every `.feature` does; below 560 px the two boxes stack.
- **The final download**: under the button, "Windows may warn you the first time: **what to click**" (to
  `#windows-warning`), then "What's new in the latest version · Source on GitHub · Discord".

### The app: Help

![Help with Pathfinder online](launch-links/app-help.png)

**Before:** Help opens on "New here, or showing a friend?", then Send feedback and What's coming.

**After:** a second full-width card under the tour card, the same shape (`.card`, `.h`, text in `--soft`, buttons
on the right):
- **Pathfinder online**
- "Talk with other players on Discord. The code is open on GitHub, where you can also open an issue. For a bug,
  **Report** on the left is still quickest: it brings the screenshot."
- Three `.btn`s with a mark each: **Discord**, **GitHub**, **eqlpathfinder.com**. Each opens the browser
  (`target="_blank"`, which the window hands to the browser).
- At 1,150 px and narrower, the buttons wrap under the text rather than squeezing it.

### The app: Settings

**Before:** Updates has Version, the update check and Join the beta.
**After:** a last row in Updates (`.setrow`): left **Open source**, under it in `.dim` 12 px "MIT license. Made by
Venun."; right, three `.link`s: **Website · GitHub · Discord**. No new card.

### The app: the tray menu

**Before:** … Sound on drops above T5, Send feedback or vote on what's next…, Fragment value ▸ …
**After:** right under Send feedback, one submenu **Pathfinder online ▸** holding **Discord**, **Source on GitHub**
and **Website**. Each opens the browser. One line in the menu, not three.

### The app: the welcome tour

**Before:** the last step's "What happens next" has three points.
**After:** a fourth: "Questions, or want to compare notes? Join the **Discord**. The code is on **GitHub**." Both are
links that open the browser. Same in first run, change character and the tour from Help.

### README.md

- The links line under the intro gains **Discord**:
  `Download for Windows · What's new · Privacy · Discord`. (The README is read on GitHub, so "GitHub" itself isn't
  needed there.)
- **Get started** gains one line after the download: "Windows may say *Windows protected your PC* the first time:
  click **More info**, then **Run anyway**. [Why](https://eqlpathfinder.com/#windows-warning)."
- **Feedback** becomes:
  - **Report a problem** in the app (or Ctrl+Shift+R) is the best way to report a bug: it brings a screenshot of the
    page, with your character's name, clock times and folders hidden, and you see it before it goes.
  - **Help → Send feedback** for ideas; **Help → Your reports** shows what happened to yours.
  - **GitHub issues** for anything you'd rather discuss in the open, and **Discord** to talk with other players.

### States

- **The Discord link must work.** A link that opens to an error is the worst first impression for a stranger. The
  invite behind `/discord` must not expire (Discord's default invite does after 7 days).
- **Offline** (the app): the browser opens and says it's offline, as for any link. Nothing in the app changes.
- **The window narrow / 150 %:** Help's card wraps its buttons under the text; the site header drops to marks alone
  under 560 px.
- **The video** (another branch): its place is the hero's screenshot, in the same frame. Shape it 16:9, 1920 × 1080,
  silent, a loop of 15 to 25 seconds, under about 8 MB as H.264 MP4, with the current Home screenshot as its poster,
  so the page looks as it does now until it plays and for anyone who blocks autoplay. The CSP will need
  `media-src 'self'`.

### Note for the engineer (theirs and the architect's to decide)

The redirects: the site's Worker is static assets only, so either a `_redirects` file in `site/public/` (Workers
static assets read it) or a rule beside the www redirect in `infra/cloudflare`. Either way `/github/*` should keep
the rest of the path (`/github/issues` → the repository's issues), so README and the app can link to issues too.
Proposed target: `github.com/venun-eq/eql-pathfinder`.

## Done when

- [x] Every site page's header shows What's new, Privacy, GitHub and Discord, the last two with their marks - seen in `site-header.png` (index, changelog, privacy, 404)
- [x] The header shows the marks alone under 560 px, on one line - seen in `site-header-narrow.png`
- [x] Every site page's footer has GitHub, Discord and "Made by Venun. Free and open source under the MIT license." - seen in `site-footer.png`
- [x] The hero shows "Free · Open source · …" and the Windows note with its link to the Windows section - seen in `site-hero.png`
- [x] Trust shows four cards, Open source among them - seen in `site-trust.png`
- [x] The Windows section shows the three steps and the two drawn boxes, numbered, with "the developer's name" and no real name - seen in `site-windows-warning.png`
- [x] The final download shows "what to click" and the GitHub and Discord links - seen in `site-final.png`
- [x] Help shows Pathfinder online under the tour card with Discord, GitHub and eqlpathfinder.com - seen in `main-help.png`
- [x] Settings' Updates card ends with the Open source row and its three links - seen in `main-settings.png`
- [x] The tour's last step has the Discord and GitHub line - seen in `welcome-ready.png`
- [x] The tray menu has Pathfinder online ▸ Discord, Source on GitHub, Website - seen in a screenshot of the open menu
- [x] The certificate's name appears nowhere in `site/`, README.md or `web/` - a check that forbids `CN=` and `Subject` on those pages (the name itself is not searched for)
- [x] README's links line, Get started and Feedback read as above

## Not doing

- The usage-counts choice in the welcome tour (its own risky branch), Kunark, and the launch video itself.
- Links in the overlay or the fight meter: they sit over the game, and a click there belongs to the game.

## Edge cases

- The `/discord` target is two lines in `site/public/_redirects` (`/discord` and `/discord/`, 302 to a non-expiring invite).
- The GitHub target is one place too: the two `/github` lines of `_redirects`. `/github/issues` keeps its path (`:splat`); both are 302 so a rename changes nothing in a copy already installed.
- The publisher line says "the developer's name" for now; `LaunchLinksTests` forbids `CN=`/`Subject` in site, README and `web/`.
- The site's CSP allows no inline style or script: marker places are classes. The icons are inline SVG in the HTML.
- App links are plain `<a target="_blank">` to eqlpathfinder.com: `WebViewSecurity` already hands user clicks to the browser. The tray opens the same three addresses with `Process.Start`.
- The video's `media-src` is not added here.

## Where

`site/partials/{header,footer}.html`, `site/index.html`, `site/site.css`, `site/public/_redirects` (new); app `web/index.html`, `web/js/settings.js`, `web/welcome.html`, `web/app.css`, `TrayIcon.cs`; `README.md`; `tests/.../LaunchLinksTests.cs`; `docs/releases/next/launch-links.md`.

**Redirects: `site/public/_redirects`, not infra/cloudflare.** One `site` deploy carries them (the site is the only thing that changes), Workers static assets read the file, and `infra/cloudflare` is a Terraform apply by hand for a one-line target. The architect may overrule.

## Reviews
- **Architect:** (11-24) sound, with one must fix before finish. Must: the release note says "there is now a Discord", so finish waits for the `/discord` invite line in `_redirects` (and the site deploy that carries it), or the note's first line drops the Discord. Should: `WebViewSecurity.OpenExternal`, now internal, checks `WebViewPolicy.IsAllowedExternalUrl` itself, so the tray and any later caller keep the eqlpathfinder.com-only rule; the "certificate's name appears nowhere" Done-when line says what `TheCertificatesNameIsNeverPrinted` really proves (it forbids `CN=`/`Subject`, not the name). Checked: the built site's `_redirects` sits beside `_headers` in the assets output, and a local `vite preview` (workerd) answers `/github` and `/github/issues/new` with 302s to the repository, keeping the path, and `/discord` with the holding page; with an invite line added, the redirect beats `discord.html`; the CSP header is on every answer and nothing new is inline. Every app link is an eqlpathfinder.com `target="_blank"` handed over by `NewWindowRequested`, nothing new is sent, so privacy.html stands. The signer's name, searched once on this PC from the installed app's signature, is in none of site/, README, CONTRIBUTING or the app's pages. The 07b4790 test change only tightened the README assertion. Redirects in `_redirects` rather than infra/cloudflare: agreed. **Fixed 11-24:** the note no longer says a Discord is open (links only); `OpenExternal` runs `WebViewPolicy.TryOpenExternal`, which checks the address itself (tested); the Done-when line says what the test forbids.
- **Designer:** (11-24, 5b4fb9b) passes, with no must fix. Checked: site header (1280, 500, 375), hero, Trust, Windows section (wide and stacked at 375), final download, footer, /discord, Help, Settings' Updates card, the tour's last step (re-rendered after e9e4eb6) and the tray's words in TrayIcon.cs. Should fix: (1) Settings: Open source sits between the update status and Join the beta; make it the card's last row, as specified. (2) Words that say more than "may", now that a signed download ran with no box at all: the Windows section's "Windows shows this blue box for a new app…" becomes "Windows can show this blue box…", and Trust's "Windows shows a publisher Microsoft has verified" becomes "signed with a publisher Microsoft has verified" (a per-user install with no box shows the publisher nowhere). Idea: the drawn boxes' title wraps "your / PC" at 1280; 20 px would keep it on one line. Not this change: at 375 px the header already wrapped to two rows before the new marks. **Fixed 11-24:** Open source is the Updates card's last row; "can show" and "signed with" worded; box titles 20 px; header wrap in docs/debt.md.

  - **Changed 11-24:** the Discord opened, so `/discord` and `/discord/` redirect (302) to the invite and the holding page (`site/discord.html`, its vite entry, its "opens soon" test) is gone. Wording now says the Discord is there: the release note's first line, and this spec's states and edge cases. The app still opens only eqlpathfinder.com/discord; `WebViewPolicy` is unchanged.
