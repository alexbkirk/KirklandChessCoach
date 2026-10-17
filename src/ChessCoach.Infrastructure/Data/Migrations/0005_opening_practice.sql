-- Practice attempts on the variation page's trouble spots (positions from your openings where you went wrong).

CREATE TABLE IF NOT EXISTS opening_practice(
  id integer primary key,
  position_key text not null,            -- FEN without the move counters
  played_uci text not null,
  correct int not null,
  at_utc text not null);                 -- ISO-8601 UTC

CREATE INDEX IF NOT EXISTS idx_opening_practice_key ON opening_practice(position_key);
