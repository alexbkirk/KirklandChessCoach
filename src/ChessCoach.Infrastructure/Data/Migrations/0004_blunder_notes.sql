-- Explanations for the daily drill's blunders, written by analysis/explain_blunders.py (same JSON shape as coach_notes).

CREATE TABLE IF NOT EXISTS blunder_notes(game_id text, ply int, note text, primary key(game_id, ply));
