"""Tests for common.py. Run from the repo root: analysis\.venv\Scripts\python -m unittest discover -s analysis/tests"""
import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from common import parse_time_classes  # noqa: E402


class ParseTimeClasses(unittest.TestCase):
    def test_rapid_is_always_included(self):
        self.assertEqual(parse_time_classes(None), ["rapid"])
        self.assertEqual(parse_time_classes(""), ["rapid"])
        self.assertEqual(parse_time_classes("blitz"), ["rapid", "blitz"])

    def test_known_classes_come_once_in_the_usual_order(self):
        self.assertEqual(parse_time_classes("bullet,blitz,rapid"), ["rapid", "blitz", "bullet"])
        self.assertEqual(parse_time_classes("blitz, BLITZ ,blitz"), ["rapid", "blitz"])

    def test_unknown_classes_are_ignored(self):
        self.assertEqual(parse_time_classes("daily,chess960,x,blitz"), ["rapid", "blitz"])


if __name__ == "__main__":
    unittest.main()
