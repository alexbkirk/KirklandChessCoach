"""Tests for motifs.py. Run from the repo root: analysis\\.venv\\Scripts\\python -m unittest discover -s analysis/tests"""
import os
import sys
import unittest

import chess

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from motifs import blunder_motifs, detect  # noqa: E402


def keys(fen, ucis, me=None):
    board = chess.Board(fen)
    line = [chess.Move.from_uci(u) for u in ucis]
    return [m["k"] for m in detect(board, line, board.turn, board.turn if me is None else me)]


class DetectTests(unittest.TestCase):
    def test_knight_fork_of_king_and_rook(self):
        found = detect(chess.Board("r3k3/8/8/3N4/8/8/8/4K3 w - - 0 1"),
                       [chess.Move.from_uci(u) for u in ["d5c7", "e8d7", "c7a8"]], chess.WHITE, chess.BLACK)
        self.assertEqual("fork", found[0]["k"])
        self.assertEqual("Knight fork", found[0]["l"])
        self.assertIn("Nc7+ hits your king on e8 and your rook on a8", found[0]["d"])

    def test_back_rank_mate(self):
        self.assertEqual("back_rank_mate", keys("6k1/5ppp/8/8/8/8/8/R5K1 w - - 0 1", ["a1a8"])[0])

    def test_smothered_mate(self):
        self.assertEqual("smothered_mate", keys("6rk/6pp/7N/8/8/8/8/6K1 w - - 0 1", ["h6f7"])[0])

    def test_other_mates_are_a_mating_attack(self):
        found = detect(chess.Board("7k/8/6K1/8/8/8/8/1Q6 w - - 0 1"), [chess.Move.from_uci("b1b8")], chess.WHITE, chess.WHITE)
        self.assertEqual("mate", found[0]["k"])
        self.assertEqual("Qb8# is mate", found[0]["d"])

    def test_attacking_a_pinned_piece(self):
        self.assertIn("pin", keys("4k3/8/2n5/1B6/3P4/8/8/4K3 w - - 0 1", ["d4d5", "e8e7", "d5c6"]))

    def test_skewer_of_king_and_queen(self):
        self.assertIn("skewer", keys("4k2q/8/8/8/8/8/8/R3K3 w - - 0 1", ["a1a8", "e8e7", "a8h8"]))

    def test_discovered_check(self):
        self.assertIn("discovered_check", keys("4k3/8/8/8/4N3/8/8/4R1K1 w - - 0 1", ["e4c3"]))

    def test_double_check(self):
        self.assertEqual("double_check", keys("4k3/8/8/8/4N3/8/8/4R1K1 w - - 0 1", ["e4f6"])[0])

    def test_removing_the_defender(self):
        self.assertIn("removing_the_defender", keys("7k/8/5n2/3r4/8/8/1B6/3R2K1 w - - 0 1", ["b2f6", "h8h7", "d1d5"]))

    def test_free_piece(self):
        self.assertEqual(["free_piece"], keys("4k3/8/8/3n4/8/8/8/3RK3 w - - 0 1", ["d1d5"]))

    def test_promotion(self):
        self.assertIn("promotion", keys("4k3/1P6/8/8/8/8/8/4K3 w - - 0 1", ["b7b8q"]))

    def test_a_quiet_line_has_no_motif(self):
        self.assertEqual([], keys(chess.STARTING_FEN, ["e2e4", "e7e5", "g1f3"]))

    def test_an_illegal_line_is_ignored(self):
        self.assertEqual([], keys(chess.STARTING_FEN, ["e2e5"]))


class BlunderMotifTests(unittest.TestCase):
    def test_hung_piece(self):
        board = chess.Board("4k3/8/7p/8/8/8/8/2B1K3 w - - 0 1")
        found = blunder_motifs(board, chess.Move.from_uci("c1g5"), chess.Move.from_uci("h6g5"), chess.WHITE)
        self.assertEqual("hung_piece", found[0]["k"])
        self.assertIn("your bishop on g5", found[0]["d"])

    def test_moved_defender(self):
        # the knight on d2 guards the bishop on e4; moving it lets the rook take
        board = chess.Board("4r1k1/8/8/8/4B3/8/3N4/4K3 w - - 0 1")
        found = blunder_motifs(board, chess.Move.from_uci("d2b3"), chess.Move.from_uci("e8e4"), chess.WHITE)
        self.assertEqual("moved_defender", found[0]["k"])
        self.assertIn("your knight on d2 was guarding your bishop on e4", found[0]["d"])

    def test_ignored_threat(self):
        # Black's queen on c6 is attacked by the knight on a5; Black plays ...Bb4+ and the knight takes the queen.
        board = chess.Board("4kb1r/3b2pp/p1q5/Nr1np3/3pN3/3P1PP1/1P1Q3P/R1B1KB1R b Kk - 1 28")
        found = blunder_motifs(board, chess.Move.from_uci("f8b4"), chess.Move.from_uci("a5c6"), chess.BLACK)
        self.assertEqual("ignored_threat", found[0]["k"])
        self.assertEqual("your queen on c6 was already attacked by their knight on a5, and Nxc6 takes it", found[0]["d"])

    def test_lost_piece_when_the_move_exposes_another_piece(self):
        # The white bishop on d3 shields the knight on d2 from the rook on d8; moving it away lets Rxd2 win the knight.
        board = chess.Board("3rk3/8/8/8/8/3B4/3N4/7K w - - 0 1")
        found = blunder_motifs(board, chess.Move.from_uci("d3a6"), chess.Move.from_uci("d8d2"), chess.WHITE)
        self.assertEqual(["lost_piece"], [m["k"] for m in found])
        self.assertEqual("Rxd2 takes your knight on d2", found[0]["d"])

    def test_an_even_trade_is_not_a_lost_piece(self):
        # Rxd2 is answered by Kxd2: the knight was defended by the king, and a rook took it, so nothing was lost cheaply.
        board = chess.Board("3rk3/8/8/8/8/3B4/3N4/3K4 w - - 0 1")
        self.assertEqual([], blunder_motifs(board, chess.Move.from_uci("d3a6"), chess.Move.from_uci("d8d2"), chess.WHITE))


if __name__ == "__main__":
    unittest.main()
