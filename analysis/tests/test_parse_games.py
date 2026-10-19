"""Tests for parse_games.py. Run from the repo root: analysis\.venv\Scripts\python -m unittest discover -s analysis/tests"""
import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from parse_games import parse_pgn_text  # noqa: E402


def game_pgn(tournament_line=""):
    return (
        '[Event "Live Chess"]\n'
        '[Site "Chess.com"]\n'
        '[Date "2026.10.01"]\n'
        '[White "alice"]\n'
        '[Black "bob"]\n'
        '[Result "1-0"]\n'
        '[TimeControl "600"]\n'
        + tournament_line +
        '\n'
        '1. e4 e5 2. Nf3 Nc6 1-0'
    )


class ParseTournament(unittest.TestCase):
    def test_tournament_header_gives_its_url(self):
        txt = game_pgn('[Tournament "https://www.chess.com/tournament/live/x"]\n')
        games = parse_pgn_text(txt, "alice")
        self.assertEqual(len(games), 1)
        self.assertEqual(games[0]["tournament"], "https://www.chess.com/tournament/live/x")

    def test_no_tournament_header_gives_none(self):
        games = parse_pgn_text(game_pgn(), "alice")
        self.assertEqual(len(games), 1)
        self.assertIsNone(games[0]["tournament"])


if __name__ == "__main__":
    unittest.main()
