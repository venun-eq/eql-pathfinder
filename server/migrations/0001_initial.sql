-- EQL Pathfinder community API: the schema as first deployed (11-16). Every statement is idempotent (IF NOT EXISTS, OR IGNORE),
-- so applying it to the database that already has these tables changes nothing.

-- Switches the operator can flip without an app update.
CREATE TABLE IF NOT EXISTS config (
  key   TEXT PRIMARY KEY,
  value TEXT NOT NULL
);
INSERT OR IGNORE INTO config (key, value) VALUES
  ('require_enrollment', '0'),     -- '1' = a new install needs an enrollment code from someone already sharing
  ('min_app', '0.8.0'),            -- older apps are told to update before sharing
  ('community_url', 'https://pub-95ff954872e74bd2a68295badc6b7ef0.r2.dev/community/v1.json');

-- A player is one person, on one or more linked PCs. No names, no emails.
CREATE TABLE IF NOT EXISTS players (
  id      TEXT PRIMARY KEY,           -- random
  created INTEGER NOT NULL,           -- unix seconds
  flagged INTEGER NOT NULL DEFAULT 0  -- 1 = rows leave every aggregate
);

-- An install is one copy of the app, known only by its public key.
CREATE TABLE IF NOT EXISTS installs (
  id          TEXT PRIMARY KEY,            -- first 32 hex of SHA-256(public key)
  player      TEXT NOT NULL,
  public_key  TEXT NOT NULL,               -- base64 SPKI, ECDSA P-256
  created     INTEGER NOT NULL,
  last_seen   INTEGER NOT NULL,
  app         TEXT,
  enrolled_by TEXT,                        -- install that made the enrollment code, when one was needed
  flagged     INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS installs_player ON installs (player);

-- One-time codes: linking a second PC (10 minutes, carries the character salt until claimed) and enrollment (24 hours).
-- Only a hash of each code is kept.
CREATE TABLE IF NOT EXISTS codes (
  hash      TEXT PRIMARY KEY,
  kind      TEXT NOT NULL,          -- 'link' | 'enroll'
  player    TEXT NOT NULL,
  install   TEXT NOT NULL,
  expires   INTEGER NOT NULL,
  uses_left INTEGER NOT NULL,
  payload   TEXT                    -- link: the player's character salt, deleted with the code
);

-- Every shared unit, one row each: a crawl or instance visit, an open-world stretch, a reincarnation, a relevel.
-- The indexed columns are copies for querying; facts holds the whole unit as the app built it (gzip JSON).
CREATE TABLE IF NOT EXISTS units (
  id          TEXT PRIMARY KEY,     -- salted code from the app; stable, so a re-send overwrites
  player      TEXT NOT NULL,
  install     TEXT NOT NULL,
  kind        TEXT NOT NULL,        -- 'crawl' | 'instance' | 'stretch' | 'reincarnation' | 'relevel'
  character   TEXT NOT NULL,        -- salted code
  server      TEXT,
  day         TEXT NOT NULL,        -- yyyy-mm-dd
  seq         INTEGER,
  zone        TEXT,
  difficulty  INTEGER,
  level_start INTEGER,
  level_end   INTEGER,
  trio        TEXT,
  race        TEXT,
  rank        INTEGER,
  schema      INTEGER NOT NULL,
  app         TEXT NOT NULL,
  engine      INTEGER NOT NULL,
  derived     TEXT,                 -- JSON: what that app version worked out
  facts       BLOB,                 -- gzip JSON of the whole unit
  received    INTEGER NOT NULL,
  quarantined INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS units_player ON units (player);
CREATE INDEX IF NOT EXISTS units_received ON units (received);

-- Per-install daily counters for the upload limits (pruned after a week).
CREATE TABLE IF NOT EXISTS usage (
  install  TEXT NOT NULL,
  day      TEXT NOT NULL,
  units    INTEGER NOT NULL DEFAULT 0,
  requests INTEGER NOT NULL DEFAULT 0,
  PRIMARY KEY (install, day)
);

-- Balance-patch dates: the class fit uses rows since the latest one.
CREATE TABLE IF NOT EXISTS patches (
  day  TEXT PRIMARY KEY,
  note TEXT
);
INSERT OR IGNORE INTO patches (day, note) VALUES ('2026-11-10', 'Fragments of Potential added; farming rates changed');

-- Feedback from the app (Help → Send feedback): what the player wrote, nothing more unless they chose to add it.
CREATE TABLE IF NOT EXISTS feedback (
  id        INTEGER PRIMARY KEY AUTOINCREMENT,
  received  INTEGER NOT NULL,
  kind      TEXT NOT NULL,            -- bug | idea | confusing | other
  text      TEXT NOT NULL,
  app       TEXT,                     -- app version
  page      TEXT,                     -- the page they were on
  contact   TEXT,                     -- a character name, only if they gave one for a reply
  voter     TEXT,                     -- the app's anonymous voter id
  errors    TEXT,                     -- their error log, only if they ticked the box
  status    TEXT NOT NULL DEFAULT 'new'   -- new | read | planned | done | wontdo
);

-- What's coming, edited by hand; the app shows it in Help with votes.
CREATE TABLE IF NOT EXISTS roadmap (
  id      TEXT PRIMARY KEY,
  title   TEXT NOT NULL,
  about   TEXT NOT NULL,
  status  TEXT NOT NULL,              -- building | next | maybe | shipped
  shipped TEXT,                       -- the version it shipped in
  sort    INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS votes (
  item   TEXT NOT NULL,
  voter  TEXT NOT NULL,               -- the app's anonymous voter id
  at     INTEGER NOT NULL,
  PRIMARY KEY (item, voter)
);
