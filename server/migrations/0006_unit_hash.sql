-- A fingerprint of what a unit holds, so a re-send of the same unit is neither written again nor counted against the
-- daily upload limit (src/sharing.ts, summaries). Rows from before have none: their next re-send writes it once.
ALTER TABLE units ADD COLUMN content_hash TEXT;
