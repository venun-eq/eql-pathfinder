-- Anonymous usage counts from the app (docs/usage.md): one row per copy per day, replaced when the app sends that day
-- again. id is the app's random usage id (not the sharing key's id, not the voter id). No address is kept. 400-day prune.
CREATE TABLE IF NOT EXISTS app_usage (
  id       TEXT NOT NULL,
  day      TEXT NOT NULL,       -- yyyy-mm-dd, the player's local day
  app      TEXT,                -- app version
  channel  TEXT,                -- stable | beta
  received INTEGER NOT NULL,    -- unix seconds, last time this day arrived
  body     TEXT NOT NULL,       -- JSON as cleaned by the Worker: counts, stats, state, errors, os, firstSeen, updatedFrom
  PRIMARY KEY (id, day)
);
CREATE INDEX IF NOT EXISTS app_usage_day ON app_usage (day);
