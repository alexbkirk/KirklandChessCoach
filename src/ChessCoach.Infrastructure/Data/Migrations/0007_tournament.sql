-- Tournament a game was played in: the chess.com tournament URL, or 'over-the-board' for hand-entered games. Null for ordinary games.

ALTER TABLE games ADD COLUMN tournament TEXT;

-- Backfill from the [Tournament "https://..."] header kept in the stored PGN.
UPDATE games
SET tournament = substr(pgn, instr(pgn, '[Tournament "') + 13,
                        instr(substr(pgn, instr(pgn, '[Tournament "') + 13), '"]') - 1)
WHERE instr(pgn, '[Tournament "') > 0
  AND instr(substr(pgn, instr(pgn, '[Tournament "') + 13), '"]') > 0;
