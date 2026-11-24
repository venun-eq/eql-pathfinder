-- What a report from "Report a problem" adds to its feedback row (one row here per feedback id that has any), and a
-- note back to the player. The screenshot itself is in the SHOTS bucket (cloudflare.config.ts), never in the database.
CREATE TABLE IF NOT EXISTS feedback_extra (
  id         INTEGER PRIMARY KEY,   -- feedback.id
  details    TEXT,                  -- window size, display scaling, page zoom and Windows version, as the app reported them
  shot       TEXT,                  -- the screenshot's key in SHOTS ("<day>/<id>.png"); NULL when none, and after 90 days
  shot_token TEXT,                  -- one upload of a screenshot for this report, from the copy that sent it; NULL once used or after a day
  reply      TEXT                   -- a note to the player, written by hand: Help → Your reports shows it beside the status
);

-- Help → Your reports: a copy's own reports, newest first.
CREATE INDEX IF NOT EXISTS feedback_voter ON feedback (voter, received);
