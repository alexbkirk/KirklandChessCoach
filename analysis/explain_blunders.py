"""Explain the daily drill's blunders (Stockfish depth 12), stored in table `blunder_notes`.

The drill's blunder pool is every blunder tagged "one_move" in `blunder_tags`. Each note has the same fields as a
coach note's explanation (see explain.py): headline, why lines, the leading motif, the better move and line.
Usage: python -I explain_blunders.py --data-dir <folder> [--depth 12] [--threads 4] [--progress jsonl]
Resumable: only blunders without a note are processed.
"""
import argparse, json, os, sys, time
import chess, chess.engine

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from common import Context, add_common_args
from explain import explain, load_time_usage, time_fact

ap = argparse.ArgumentParser()
add_common_args(ap)
ap.add_argument("--depth", type=int, default=12)
ap.add_argument("--threads", type=int, default=4)
a = ap.parse_args()
ctx = Context(a)
db = ctx.connect()
todo = db.execute("""select m.game_id, m.ply, m.fen_before, m.uci, m.san, m.clk from blunder_tags t
    join moves m on m.game_id = t.game_id and m.ply = t.ply
    where t.tags like '%"one_move"%'
      and not exists (select 1 from blunder_notes n where n.game_id = t.game_id and n.ply = t.ply)
    order by m.game_id, m.ply""").fetchall()
if not todo:
    ctx.progress("explain", 0, 0, "Every drill blunder already has a note")
    sys.exit(0)

spent, usual = load_time_usage(db)
eng = chess.engine.SimpleEngine.popen_uci(ctx.stockfish)
eng.configure({"Threads": a.threads, "Hash": 256})
lim = chess.engine.Limit(depth=a.depth)
t0 = time.time()
for i, (gid, ply, fen, uci, san, clk) in enumerate(todo, 1):
    b = chess.Board(fen); mv = chess.Move.from_uci(uci)
    before = eng.analyse(b, lim)
    after_b = b.copy(); after_b.push(mv)
    after = eng.analyse(after_b, lim)
    note = explain(eng, b, mv, before, after)
    t = time_fact(spent, usual, gid, ply, san, clk)
    if t: note["why"].append(t)
    db.execute("insert or replace into blunder_notes values (?,?,?)", (gid, ply, json.dumps(note, ensure_ascii=False)))
    if i % 25 == 0 or i == len(todo):
        db.commit(); el = time.time() - t0
        ctx.progress("explain", i, len(todo), f"Explained {i} of {len(todo)} drill blunders (eta {el / i * (len(todo) - i):.0f}s)")
eng.quit()
ctx.progress("explain", len(todo), len(todo), f"{len(todo)} drill blunders explained")
