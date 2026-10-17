"""Tests for explain.py. Run from the repo root: analysis\\.venv\\Scripts\\python -m unittest discover -s analysis/tests"""
import os
import sqlite3
import sys
import unittest

import chess
import chess.engine

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from explain import chances, explain, load_time_usage, stage, time_fact  # noqa: E402


def info(cp, pov, pv):
    return {"score": chess.engine.PovScore(chess.engine.Cp(cp), pov), "pv": [chess.Move.from_uci(u) for u in pv]}


class FakeEngine:
    """Answers the null-move threat check with a fixed score for the side to move."""

    def __init__(self, cp_for_side_to_move):
        self.cp = cp_for_side_to_move
        self.calls = 0

    def analyse(self, board, limit):
        self.calls += 1
        return {"score": chess.engine.PovScore(chess.engine.Cp(self.cp), board.turn), "pv": []}


class ExplainTests(unittest.TestCase):
    def test_chances_are_half_at_zero_and_near_certain_for_mate(self):
        self.assertAlmostEqual(50, chances(0))
        self.assertGreater(chances(10000), 99.9)
        self.assertLess(chances(-10000), 0.1)

    def test_a_move_that_walks_into_a_fork_names_it_and_says_the_move_created_it(self):
        # Black (king e8, rook a8) plays Ra6, and White's knight on d5 forks king and rook with Nc7+.
        board = chess.Board("r3k3/8/8/3N4/8/8/8/4K3 b - - 0 1")
        move = chess.Move.from_uci("a8a6")
        before = info(0, chess.BLACK, ["a8a7"])
        after = info(-500, chess.BLACK, ["d5c7", "e8d7", "c7a6"])
        eng = FakeEngine(cp_for_side_to_move=-20)   # after a null move, White (to move) has nothing

        note = explain(eng, board, move, before, after)

        self.assertEqual("allowed", note["side"])
        self.assertEqual("fork", note["motif"]["k"])
        self.assertEqual("Ra6 allows a knight fork. Nc7+ hits your king on e8 and your rook on a6.", note["headline"])
        self.assertIn("Before Ra6 they had no such shot. Your move created it.", note["why"])
        self.assertEqual("Winning chances: 50% before, 14% after.", note["why"][-1])
        self.assertEqual("Ra7", note["better"])
        self.assertEqual(1, eng.calls)

    def test_a_threat_that_was_already_there_is_called_out(self):
        # Nc7+ already forks king e8 and rook a8; Black plays a pawn move instead of dealing with it.
        board = chess.Board("r3k3/7p/8/3N4/8/8/8/4K3 b - - 0 1")
        before = info(-100, chess.BLACK, ["e8d7"])
        after = info(-500, chess.BLACK, ["d5c7", "e8d7", "c7a8"])
        eng = FakeEngine(cp_for_side_to_move=500)   # White already had the fork

        note = explain(eng, board, chess.Move.from_uci("h7h6"), before, after)

        self.assertEqual("h6 allows a knight fork. Nc7+ hits your king on e8 and your rook on a8.", note["headline"])
        self.assertIn("The threat was already on the board before h6; your move did nothing about it.", note["why"])

    def test_a_quiet_loss_reads_as_costly_without_claiming_the_game_was_thrown(self):
        board = chess.Board()
        before = info(30, chess.WHITE, ["e2e4"])
        after = info(-90, chess.WHITE, ["e7e5"])

        note = explain(FakeEngine(0), board, chess.Move.from_uci("f2f3"), before, after)

        self.assertEqual("positional", note["side"])
        self.assertEqual("f3 costs you: e5 follows, and e4 was the better move.", note["headline"])


class TimeTests(unittest.TestCase):
    def setUp(self):
        self.db = sqlite3.connect(":memory:")
        self.db.executescript("""
            create table games(id text, timeControl text, timeClass text);
            create table moves(game_id text, ply int, is_mine int, clk real);
            insert into games values ('g', '600+5', 'rapid'), ('d', '1/86400', 'daily');
            insert into moves values ('g', 1, 1, 598), ('g', 3, 1, 590), ('g', 5, 1, 594), ('d', 1, 1, 80000);
        """)

    def test_time_spent_counts_the_increment_and_skips_daily_games(self):
        spent, usual = load_time_usage(self.db)

        self.assertEqual({("g", 1): 7, ("g", 3): 13, ("g", 5): 1}, spent)
        self.assertEqual({"opening": 7}, usual)

    def test_the_time_fact_compares_with_your_usual_and_flags_a_low_clock(self):
        spent, usual = {("g", 61): 0.4}, {"late middlegame and endgame": 4}

        self.assertEqual("You played Kg2 instantly; your usual in the late middlegame and endgame is 4s, and you had 23s left.",
                         time_fact(spent, usual, "g", 61, "Kg2", 23))
        self.assertIsNone(time_fact(spent, usual, "g", 63, "Kh1", 20))
        self.assertEqual("middlegame", stage(13))


if __name__ == "__main__":
    unittest.main()
