-- New sharing players per address per day (Worker newPlayerLimit, config max_new_players_per_ip; off while invite-only).
-- code = first 32 hex of SHA-256("eqlp-ip:" + the IP_SECRET Worker secret + ":" + day + ":" + address): the address is never stored,
-- and a day's codes can't be matched with another day's. Pruned after 2 days by the daily cron.
CREATE TABLE IF NOT EXISTS register_ips (
  code TEXT NOT NULL,
  day  TEXT NOT NULL,
  n    INTEGER NOT NULL,
  PRIMARY KEY (code, day)
);
