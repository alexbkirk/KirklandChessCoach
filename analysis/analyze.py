"""Run Stockfish over games in xkirke_games.db and store per-move evaluations.

Usage: python -I analyze.py --data-dir <folder> [--limit N] [--depth D] [--threads T] [--progress jsonl]
Resumable: games already in `game_analysis` are skipped.
"""
import argparse, io, os, sys, time
import chess, chess.engine, chess.pgn

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from common import Context, add_common_args

MATE = 10000

ap = argparse.ArgumentParser()
add_common_args(ap)
ap.add_argument("--limit", type=int, default=0)
ap.add_argument("--depth", type=int, default=12)
ap.add_argument("--threads", type=int, default=2)
a = ap.parse_args()
ctx = Context(a)
ENGINE = ctx.stockfish
db = ctx.connect()

def classify(loss):
    return "best" if loss <= 10 else "good" if loss < 30 else "inaccuracy" if loss < 80 else "mistake" if loss < 200 else "blunder"

todo = db.execute("select id, color, pgn from games where id not in (select game_id from game_analysis) order by date desc, time desc").fetchall()
if a.limit: todo = todo[:a.limit]
eng = chess.engine.SimpleEngine.popen_uci(ENGINE)
eng.configure({"Threads": a.threads, "Hash": 128})
lim = chess.engine.Limit(depth=a.depth)
t0 = time.time()

def score(info):  # white POV centipawns
    return info["score"].white().score(mate_score=MATE)

for gi, (gid, color, pgn) in enumerate(todo, 1):
    game = chess.pgn.read_game(io.StringIO(pgn))
    board = game.board()
    node = game
    me_white = color == "white"
    info = eng.analyse(board, lim)
    ev, best = score(info), (info.get("pv") or [None])[0]
    rows, losses = [], []
    for ply, mv in enumerate(game.mainline_moves(), 1):
        white_moved = board.turn == chess.WHITE
        fen, san = board.fen(), board.san(mv)
        best_san = board.san(best) if best else None
        board.push(mv)
        info = eng.analyse(board, lim)
        ev2 = score(info)
        mine = white_moved == me_white
        loss = max(0, (ev - ev2) if white_moved else (ev2 - ev))
        loss = min(loss, 1000)
        cls = classify(loss)
        node = node.next() if node else None
        rows.append((gid, ply, "white" if white_moved else "black", int(mine), san, mv.uci(), fen,
                     best.uci() if best else None, best_san, ev, ev2, loss, cls, 0, node.clock() if node else None))
        if mine: losses.append((loss, cls))
        ev, best = ev2, (info.get("pv") or [None])[0]
        if board.is_game_over(): break
    db.executemany("insert or replace into moves(game_id,ply,color,is_mine,san,uci,fen_before,best_uci,best_san,eval_before,eval_after,cp_loss,class,is_last,clk) values (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)", rows)
    db.execute("update moves set is_last=1 where game_id=? and ply=(select max(ply) from moves where game_id=?)", (gid, gid))
    n = len(losses)
    cnt = lambda c: sum(1 for _, k in losses if k == c)
    db.execute("insert or replace into game_analysis values (?,?,?,?,?,?,?)",
               (gid, a.depth, sum(l for l, _ in losses) / n if n else None, n, cnt("inaccuracy"), cnt("mistake"), cnt("blunder")))
    db.commit()
    el = time.time() - t0
    ctx.progress("analyze", gi, len(todo), f"Analysed game {gi} of {len(todo)} ({gid}, {len(rows)} plies, eta {el/gi*(len(todo)-gi):.0f}s)")
eng.quit()
