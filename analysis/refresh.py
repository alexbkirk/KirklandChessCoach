"""Refresh the chess data (rapid games only): fetch new games, analyse them, tag blunders, regenerate coach notes.

Usage: python -I refresh.py --data-dir <folder> [--user xkirke] [--skip-coach] [--progress jsonl]
Incremental: only months from the latest stored game onward are downloaded, only rapid games not already in
xkirke_games.db are added (chess.com's rapid = 10 minutes and up), and Stockfish only analyses games that have no
analysis yet. Every stage is resumable, so cancelling (killing) this process is safe.
"""
import argparse
import json
import os
import subprocess
import sys
import time
import urllib.request

H = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, H)
from common import Context, add_common_args
from parse_games import parse_pgn_text

ap = argparse.ArgumentParser()
add_common_args(ap)
ap.add_argument("--user", default="xkirke")
ap.add_argument("--skip-coach", action="store_true", help="don't regenerate coach notes")
a = ap.parse_args()
ctx = Context(a)
UA = f"chess-coach-refresh ({a.user}; contact: alexbkirk@gmail.com)"
API = f"https://api.chess.com/pub/player/{a.user}/games"


def get(url, retries=3):
    for i in range(retries):
        try:
            return urllib.request.urlopen(urllib.request.Request(url, headers={"User-Agent": UA}), timeout=60).read().decode("utf-8")
        except Exception:
            if i == retries - 1:
                raise
            time.sleep(3 * (i + 1))


def run(script, *args):
    """Run a sibling script; its stdout (including JSON progress lines) passes straight through."""
    cmd = [sys.executable, "-I", os.path.join(H, script), "--data-dir", ctx.data_dir, "--progress", "jsonl" if ctx.jsonl else "text", *args]
    r = subprocess.run(cmd, cwd=H)
    if r.returncode:
        sys.exit(f"{script} failed (exit {r.returncode})")


db = ctx.connect()
known = {r[0] for r in db.execute("select id from games")}
last = db.execute("select max(date) from games").fetchone()[0] or "0000-00"
start = last[:7]   # re-fetch the month of the latest game: it may have gained games since
ctx.progress("fetch", 0, None, f"Latest stored game: {last}. Checking chess.com from {start}.")
archives = json.loads(get(f"{API}/archives"))["archives"]
months = [u for u in archives if "/".join(u.rsplit("/", 2)[-2:]).replace("/", "-") >= start]

new = []
for i, u in enumerate(months, 1):
    for g in parse_pgn_text(get(u + "/pgn"), a.user):
        if g["timeClass"] == "rapid" and g["id"] not in known:
            known.add(g["id"])
            new.append(g)
    ctx.progress("fetch", i, len(months), f"Downloaded month {i} of {len(months)}")
    time.sleep(0.3)
ctx.progress("fetch", len(months), len(months), f"{len(new)} new rapid game(s).")

if new:
    cols = [r[1] for r in db.execute("pragma table_info(games)")]
    db.executemany(f"insert into games ({','.join(cols)}) values ({','.join('?' * len(cols))})", [[g[c] for c in cols] for g in new])
    db.commit()
    with open(ctx.pgn_path, "a", encoding="utf-8") as f:
        f.write("\n" + "\n\n".join(g["pgn"] for g in new) + "\n")

pending = db.execute("select count(*) from games where id not in (select game_id from game_analysis)").fetchone()[0]
db.close()
if pending:
    run("analyze.py", "--threads", "4")
    run("tag_blunders.py", "--threads", "4")   # tags only blunders that have no tags yet
if (new or pending) and not a.skip_coach:
    run("coach.py", "--top", "100")
elif a.skip_coach:
    ctx.progress("coach", None, None, "Coach notes skipped.")
else:
    ctx.progress("coach", None, None, "No new games: coach notes unchanged.")

db = ctx.connect()
total = db.execute("select count(*) from games").fetchone()[0]
latest = db.execute("select max(date) from games").fetchone()[0]
ctx.progress("done", 1, 1, f"Done. {len(new)} new game(s) added; {total} games total; latest game {latest}.")
