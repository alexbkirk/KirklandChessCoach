-- Training progress owned by the app (replaces the browser's localStorage).

CREATE TABLE IF NOT EXISTS puzzle_progress(
  position_id text primary key,          -- '<game_id>_<ply>'
  box int not null,                      -- Leitner box 0-6
  due_utc text not null,                 -- ISO-8601 UTC
  tries int not null,
  correct int not null,
  last_seen_utc text);

CREATE TABLE IF NOT EXISTS puzzle_attempts(
  id integer primary key,
  position_id text not null,
  played_uci text,
  correct int not null,
  gave_up int not null,
  hint_used int not null,
  at_utc text not null);

CREATE TABLE IF NOT EXISTS drill_days(
  day text primary key,                  -- local date, yyyy-MM-dd
  seed int not null);

CREATE TABLE IF NOT EXISTS drill_slots(
  day text not null,
  idx int not null,
  position_id text not null,
  format text not null,                  -- hang | cct | safe
  state_json text not null,
  done int not null,
  score int not null,
  primary key(day, idx));

CREATE TABLE IF NOT EXISTS app_settings(key text primary key, value text);
