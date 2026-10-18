-- Your own notes when reviewing a game. They live apart from the engine analysis, so re-analysing a game never loses them.

CREATE TABLE IF NOT EXISTS game_review_notes(
  game_id text primary key,
  note text not null,
  updated_at text not null);             -- ISO-8601 UTC

CREATE TABLE IF NOT EXISTS move_annotations(
  game_id text not null,
  ply int not null,
  glyph text,                            -- !!, !, !?, ?!, ? or ??; null when only a note
  note text not null default '',
  updated_at text not null,              -- ISO-8601 UTC
  primary key(game_id, ply));
