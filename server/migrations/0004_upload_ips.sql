-- One character per game server per connection per day (Worker oneCharacterPerServer; config one_character_per_server
-- "0" turns it off). code = the connection's daily code (see 002); character = the upload's salted character code.
-- Pruned after 2 days; deleted with the player (DELETE /v1/me) and when an install joins another player.
CREATE TABLE IF NOT EXISTS upload_ips (
  code      TEXT NOT NULL,
  day       TEXT NOT NULL,
  server    TEXT NOT NULL,
  character TEXT NOT NULL,
  player    TEXT NOT NULL,
  PRIMARY KEY (code, day, server)
);
