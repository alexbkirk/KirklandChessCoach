"""Shared helpers for the analysis scripts: data-folder paths, database connection and progress reporting.

Every script takes --data-dir (or the CHESSCOACH_DATA_DIR environment variable) and --progress {text,jsonl}.
With --progress jsonl each progress update is one JSON object per line on stdout, which the app reads:
    {"stage": "analyze", "done": 12, "total": 40, "message": "Analysing game 12 of 40"}
The schema is owned by the app's migrations: these scripts only read and write rows.
"""
import json
import os
import sqlite3
import sys

BUSY_TIMEOUT_MS = 5000


TIME_CLASSES = ("rapid", "blitz", "bullet")


def parse_time_classes(text):
    """The time classes to import from a comma-separated list: only known ones, once each, and always rapid."""
    wanted = {part.strip().lower() for part in (text or "").split(",")} | {"rapid"}
    return [c for c in TIME_CLASSES if c in wanted]


def add_common_args(parser):
    default = os.environ.get("CHESSCOACH_DATA_DIR")
    parser.add_argument("--data-dir", default=default, required=default is None,
                        help="folder holding xkirke_games.db and the engine folder (or set CHESSCOACH_DATA_DIR)")
    parser.add_argument("--progress", choices=["text", "jsonl"], default="text")


class Context:
    def __init__(self, args):
        self.data_dir = os.path.abspath(args.data_dir)
        self.jsonl = args.progress == "jsonl"
        self.db_path = os.path.join(self.data_dir, "xkirke_games.db")
        self.pgn_path = os.path.join(self.data_dir, "xkirke_games.pgn")
        engines = os.path.join(self.data_dir, "engine")
        self.stockfish = os.path.join(engines, "stockfish", "stockfish-windows-x86-64-universal.exe")
        self.lc0 = os.path.join(engines, "lc0", "lc0.exe")
        self.maia = os.path.join(engines, "maia", "maia-1100.pb.gz")

    def connect(self):
        if not os.path.exists(self.db_path):
            sys.exit(f"Database not found: {self.db_path} (start the app once so it can create/migrate it)")
        db = sqlite3.connect(self.db_path, timeout=BUSY_TIMEOUT_MS / 1000)
        db.execute(f"pragma busy_timeout = {BUSY_TIMEOUT_MS}")
        return db

    def progress(self, stage, done=None, total=None, message=""):
        if self.jsonl:
            print(json.dumps({"stage": stage, "done": done, "total": total, "message": message}), flush=True)
        else:
            print(message, flush=True)
