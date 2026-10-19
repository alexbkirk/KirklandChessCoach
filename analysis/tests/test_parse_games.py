"""Tests for parse_games.py. Run from the repo root: analysis\.venv\Scripts\python -m unittest discover -s analysis/tests"""
import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from parse_games import parse_pgn_text, tclass  # noqa: E402


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


class TimeClass(unittest.TestCase):
    def test_usual_time_controls(self):
        self.assertEqual(tclass("60"), "bullet")
        self.assertEqual(tclass("180"), "blitz")
        self.assertEqual(tclass("600"), "rapid")
        self.assertEqual(tclass("900+10"), "rapid")
        self.assertEqual(tclass("1/86400"), "daily")

    def test_a_missing_or_unreadable_time_control_is_other_not_a_crash(self):
        for tc in ("", "-", "?", "abc"):
            self.assertEqual(tclass(tc), "other")

    def test_a_game_without_a_readable_time_control_still_parses(self):
        pgn = '[Event "Live Chess"]\n[White "me"]\n[Black "you"]\n[Result "1-0"]\n[Date "2026.01.01"]\n[TimeControl "-"]\n\n1. e4 e5 1-0'
        games = parse_pgn_text(pgn, "me")
        self.assertEqual([g["timeClass"] for g in games], ["other"])


class UnreadableHeaders(unittest.TestCase):
    def _pgn(self, extra):
        return '[Event "Live Chess"]\n[White "me"]\n[Black "you"]\n[Result "1-0"]\n' + extra + '\n\n1. e4 e5 1-0'

    def test_a_rating_that_is_not_a_number_is_zero(self):
        games = parse_pgn_text(self._pgn('[Date "2026.01.01"]\n[WhiteElo "-"]\n[BlackElo "?"]'), "me")
        self.assertEqual((games[0]["myRating"], games[0]["oppRating"]), (0, 0))

    def test_a_game_with_no_date_is_skipped_not_a_crash(self):
        self.assertEqual(parse_pgn_text(self._pgn('[WhiteElo "1500"]'), "me"), [])

    def test_a_placeholder_date_is_skipped(self):
        self.assertEqual(parse_pgn_text(self._pgn('[Date "????.??.??"]'), "me"), [])

    def test_a_real_utc_date_is_used_and_a_placeholder_one_falls_back_to_the_date(self):
        good = parse_pgn_text(self._pgn('[Date "2026.01.02"]\n[UTCDate "2026.01.03"]'), "me")
        self.assertEqual(good[0]["date"], "2026-01-03")
        fallback = parse_pgn_text(self._pgn('[Date "2026.01.02"]\n[UTCDate "????.??.??"]'), "me")
        self.assertEqual(fallback[0]["date"], "2026-01-02")


if __name__ == "__main__":
    unittest.main()
