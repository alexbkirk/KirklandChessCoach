-- Baseline: the schema the Python pipeline created before the app existed.
-- IF NOT EXISTS makes this a no-op on the existing database and builds an identical empty one for tests.
-- Conventions: evaluations are centipawns from White's point of view; mate scores are +/-10000 minus the mate distance;
-- cp_loss is capped at 1000; is_last marks the final move of a game; clk is seconds left after the move.

CREATE TABLE IF NOT EXISTS games (id, url, date, time, color, result, ending, timeControl, timeClass, myRating, oppRating, opponent, eco, opening, family, plies, moves, pgn, primary key(id));

CREATE TABLE IF NOT EXISTS moves(
  game_id text, ply int, color text, is_mine int, san text, uci text,
  fen_before text, best_uci text, best_san text,
  eval_before int, eval_after int, cp_loss int, class text, is_last int default 0, clk real,
  primary key(game_id, ply));

CREATE TABLE IF NOT EXISTS game_analysis(
  game_id text primary key, depth int, my_acpl real, n_my_moves int,
  inaccuracies int, mistakes int, blunders int);

CREATE TABLE IF NOT EXISTS blunder_tags(game_id text, ply int, tags text, primary key(game_id, ply));   -- tags: JSON array

CREATE TABLE IF NOT EXISTS coach_notes(game_id text, ply int, note text, primary key(game_id, ply));    -- note: JSON object
