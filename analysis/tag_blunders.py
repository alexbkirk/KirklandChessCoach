"""Tag every blunder in xkirke_games.db (table `blunder_tags`) using Stockfish at depth 12.

Usage: python -I tag_blunders.py --data-dir <folder> [--depth 12] [--threads 4] [--progress jsonl]   (resumable: only untagged blunders are processed)
"""
import argparse, json, os, sys, time
import chess, chess.engine

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from common import Context, add_common_args
from tagging import tags_for

ap = argparse.ArgumentParser()
add_common_args(ap)
ap.add_argument("--depth", type=int, default=12)
ap.add_argument("--threads", type=int, default=4)
a = ap.parse_args()
ctx = Context(a)
db = ctx.connect()
todo = db.execute("""select game_id, ply, fen_before, uci from moves m where is_mine=1 and is_last=0 and class='blunder'
  and not exists (select 1 from blunder_tags t where t.game_id=m.game_id and t.ply=m.ply) order by game_id, ply""").fetchall()
eng = chess.engine.SimpleEngine.popen_uci(ctx.stockfish)
eng.configure({"Threads": a.threads, "Hash": 256})
lim = chess.engine.Limit(depth=a.depth)
t0 = time.time()
for i, (gid, ply, fen, uci) in enumerate(todo, 1):
    b = chess.Board(fen); mv = chess.Move.from_uci(uci); mover = b.turn
    before = eng.analyse(b, lim)
    b2 = b.copy(); b2.push(mv)
    after = eng.analyse(b2, lim)
    db.execute("insert or replace into blunder_tags values (?,?,?)", (gid, ply, json.dumps(tags_for(b, mv, before, after, mover))))
    if i % 200 == 0:
        db.commit(); el = time.time() - t0
        ctx.progress("tag", i, len(todo), f"Tagged {i} of {len(todo)} blunders (eta {el / i * (len(todo) - i):.0f}s)")
db.commit(); eng.quit()
ctx.progress("tag", len(todo), len(todo), f"{len(todo)} blunders tagged")
