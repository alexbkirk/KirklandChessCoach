-- Indexes for the dashboard queries (recent games, per time class, blunder lookups).

CREATE INDEX IF NOT EXISTS idx_games_date ON games(date, time);
CREATE INDEX IF NOT EXISTS idx_moves_mine_class ON moves(is_mine, class);
CREATE INDEX IF NOT EXISTS idx_puzzle_attempts_position ON puzzle_attempts(position_id);
