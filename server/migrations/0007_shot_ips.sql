-- Report screenshots per connection per day (Worker shotLimit, config max_shots_per_ip, default 5; "0" turns it off).
-- code = the connection's daily code (see 0003); the address is never stored. Pruned after 2 days by the daily cron.
CREATE TABLE IF NOT EXISTS shot_ips (
  code TEXT NOT NULL,
  day  TEXT NOT NULL,
  n    INTEGER NOT NULL,
  PRIMARY KEY (code, day)
);
