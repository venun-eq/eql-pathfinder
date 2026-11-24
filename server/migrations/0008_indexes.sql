-- Indexes for daily pruning, screenshot quota checks, install cleanup, and code lookups.
-- Every statement is idempotent (IF NOT EXISTS).
CREATE INDEX IF NOT EXISTS units_day ON units (day);
CREATE INDEX IF NOT EXISTS units_install ON units (install);
CREATE INDEX IF NOT EXISTS feedback_received ON feedback (received);
CREATE INDEX IF NOT EXISTS codes_player ON codes (player);
CREATE INDEX IF NOT EXISTS codes_expires ON codes (expires);
