-- Bytes of shared unit facts + derived JSON stored per install per day (Worker MAX_BYTES_PER_DAY and global daily fuses).
-- SQLite has no ADD COLUMN IF NOT EXISTS; D1's migrations table applies each numbered migration once.
ALTER TABLE usage ADD COLUMN bytes INTEGER NOT NULL DEFAULT 0;
CREATE INDEX IF NOT EXISTS usage_day ON usage (day);

-- Feedback reports and roadmap votes per connection per day (Worker feedbackLimit and voteLimit; "0" turns either off).
-- code = the connection's daily HMAC code (see 0003); the address is never stored. Pruned after 2 days by the daily cron.
CREATE TABLE IF NOT EXISTS feedback_ips (
  code     TEXT NOT NULL,
  day      TEXT NOT NULL,
  feedback INTEGER NOT NULL DEFAULT 0,
  votes    INTEGER NOT NULL DEFAULT 0,
  PRIMARY KEY (code, day)
);
