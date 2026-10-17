"""Generate coach notes for the costliest blunders (Stockfish + Maia 1100), stored in table `coach_notes`.

Selection: balanced-position, non-final-move blunders, ranked by centipawn loss (capped at 1000),
ties broken by most recent game. Usage: python -I coach.py --data-dir <folder> [--top 100] [--depth 16] [--progress jsonl]
"""
import argparse, json, os, re, sqlite3, subprocess, sys
import chess, chess.engine

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from common import Context, add_common_args
from tagging import tags_for

MAIA_LEVEL = 1100
VAL = {chess.PAWN: 1, chess.KNIGHT: 3, chess.BISHOP: 3, chess.ROOK: 5, chess.QUEEN: 9, chess.KING: 100}
NAME = {chess.PAWN: "pawn", chess.KNIGHT: "knight", chess.BISHOP: "bishop", chess.ROOK: "rook", chess.QUEEN: "queen", chess.KING: "king"}

ap = argparse.ArgumentParser()
add_common_args(ap)
ap.add_argument("--top", type=int, default=100)
ap.add_argument("--depth", type=int, default=16)
a = ap.parse_args()
ctx = Context(a)
SF, LC0, MAIA = ctx.stockfish, ctx.lc0, ctx.maia

db = ctx.connect(); db.row_factory = sqlite3.Row
db.execute("delete from coach_notes")
rows = db.execute("""select m.game_id, m.ply, m.color, m.san, m.uci, m.fen_before, m.eval_before, m.cp_loss, m.clk,
    g.family, g.timeClass from moves m join games g on g.id = m.game_id
  where m.is_mine=1 and m.is_last=0 and m.class='blunder'
    and ((m.color='white' and m.eval_before>=-100) or (m.color='black' and m.eval_before<=100))
  order by m.cp_loss desc, g.date desc, g.time desc limit ?""", (a.top * 4,)).fetchall()   # spare candidates: some fail the depth-16 re-check

# --- Maia: policy head only (1 node), parse per-move probabilities ---------------------------
maia = subprocess.Popen([LC0, f"--weights={MAIA}"], stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, text=True, cwd=os.path.dirname(LC0))
def send(s): maia.stdin.write(s + "\n"); maia.stdin.flush()
send("uci")
while "uciok" not in maia.stdout.readline(): pass
send("setoption name VerboseMoveStats value true"); send("isready")
while "readyok" not in maia.stdout.readline(): pass
PROB = re.compile(r"info string (\S+)\s+\(\s*\d+\s*\).*?\(P:\s*([\d.]+)%\)")
def maia_policy(fen):
    send(f"position fen {fen}"); send("go nodes 1")
    probs = {}
    while True:
        line = maia.stdout.readline()
        m = PROB.match(line.strip())
        if m and m.group(1) != "node": probs[m.group(1)] = float(m.group(2))
        if line.startswith("bestmove"): return probs

eng = chess.engine.SimpleEngine.popen_uci(SF); eng.configure({"Threads": 4, "Hash": 256})
lim = chess.engine.Limit(depth=a.depth)

def fmt_cp(score):
    if score.is_mate(): return f"mate in {abs(score.mate())}" if score.mate() > 0 else f"getting mated in {abs(score.mate())}"
    v = score.score() / 100
    return f"{v:+.1f}"

def line_san(board, pv, n):
    b = board.copy(); out = []
    for mv in pv[:n]:
        out.append(b.san(mv)); b.push(mv)
    return " ".join(out)

def hanging(board, sq, color):
    p = board.piece_at(sq)
    if not p: return False
    att = board.attackers(not color, sq); dfn = board.attackers(color, sq)
    if not att: return False
    return not dfn or min(VAL[board.piece_at(x).piece_type] for x in att) < VAL[p.piece_type]

def note_for(r):
    b = chess.Board(r["fen_before"]); mover = b.turn; mv = chess.Move.from_uci(r["uci"])
    san = r["san"]; ply = r["ply"]; movenum = (ply + 1) // 2
    before = eng.analyse(b, lim)
    best = before["pv"][0]; best_san = b.san(best); sb = before["score"].pov(mover)
    # puzzle answers: every move within 50 centipawns of the best (mate scores compared as +/-10000)
    multi = eng.analyse(b, lim, multipv=5)
    top = multi[0]["score"].pov(mover).score(mate_score=10000)
    accept = [i["pv"][0].uci() for i in multi if i.get("pv") and i["score"].pov(mover).score(mate_score=10000) >= top - 50]
    if best.uci() not in accept: accept.insert(0, best.uci())
    best_line = line_san(b, before["pv"], 4)
    best_is_cap = b.is_capture(best); best_cap_piece = b.piece_at(best.to_square) if best_is_cap and not b.is_en_passant(best) else None
    b2 = b.copy(); b2.push(best); best_check = b2.is_check(); best_mate = b2.is_checkmate()
    after_b = b.copy(); after_b.push(mv)
    after = eng.analyse(after_b, lim); sa = after["score"].pov(mover)
    if min(1000, max(0, sb.score(mate_score=10000) - sa.score(mate_score=10000))) < 200: return None   # not a blunder at higher depth
    reply = (after.get("pv") or [None])[0]
    cont = line_san(after_b, after["pv"], 4) if reply else ""
    moved = b.piece_at(mv.from_square); got = b.piece_at(mv.to_square)
    why, kind = [], "eval"
    if sa.is_mate() and sa.mate() < 0:
        n = abs(sa.mate()); kind = "allowed_mate"
        why.append(f"After {san}, your opponent has a forced checkmate in {n}, starting with {after_b.san(reply)}. Your king had no safe answer.")
    elif sb.is_mate() and sb.mate() > 0 and not (sa.is_mate() and sa.mate() > 0):
        kind = "missed_mate"
        why.append(f"You had a forced checkmate in {sb.mate()} with {best_san} and played {san} instead.")
    elif reply and hanging(after_b, mv.to_square, mover) and after_b.is_capture(reply) and reply.to_square == mv.to_square:
        kind = "hung_piece"
        why.append(f"{san} leaves your {NAME[moved.piece_type]} on {chess.square_name(mv.to_square)} where it can simply be taken by {after_b.san(reply)}" + (f" (you won a {NAME[got.piece_type]}, but gave up more)." if got and VAL[got.piece_type] < VAL[moved.piece_type] else "."))
    elif reply and after_b.is_capture(reply) and not after_b.is_en_passant(reply):
        cp = after_b.piece_at(reply.to_square)
        if cp and VAL[cp.piece_type] >= 3:
            kind = "left_hanging"
            why.append(f"After {san}, your {NAME[cp.piece_type]} on {chess.square_name(reply.to_square)} is left unprotected and {after_b.san(reply)} wins it.")
    if kind == "eval" and best_cap_piece and VAL[best_cap_piece.piece_type] >= 3 and not b.is_capture(mv):
        kind = "missed_capture"
        why.append(f"{best_san} wins a free {NAME[best_cap_piece.piece_type]}; {san} let that chance go.")
    if kind == "eval" and (best_check or best_mate):
        kind = "missed_tactic"
        why.append(f"{best_san} {'checkmates' if best_mate else 'gives check and starts a winning attack'}; {san} missed it.")
    if kind == "eval":
        why.append(f"{san} lets your opponent take over: after {after_b.san(reply) if reply else 'their reply'} they are clearly better.")
    why.append(f"Evaluation (your side): {fmt_cp(sb)} before, {fmt_cp(sa)} after {san}." + (f" Likely continuation: {cont}." if cont and kind != "allowed_mate" else ""))
    # --- Maia
    probs = maia_policy(r["fen_before"])
    top = sorted(probs.items(), key=lambda kv: -kv[1])[:3]
    topsan = [(b.san(chess.Move.from_uci(u)), p) for u, p in top]
    p_played, p_best = probs.get(r["uci"], 0.0), probs.get(best.uci(), 0.0)
    if p_played >= 20:
        mtxt = f"Players around {MAIA_LEVEL} rating choose {san} {p_played:.0f}% of the time here, so this is a common trap at your level, not a random slip."
    elif top and top[0][0] == best.uci():
        mtxt = f"Maia ({MAIA_LEVEL}) would most likely play {best_san} too ({p_best:.0f}%): this was a move within your level to find."
    elif p_best < 10:
        mtxt = f"The best move {best_san} is one that players around {MAIA_LEVEL} rarely find (only {p_best:.0f}%), so it is hard to see; the rest of the lesson is avoiding {san}."
    else:
        mtxt = f"Maia ({MAIA_LEVEL}) would most likely play {topsan[0][0]} ({topsan[0][1]:.0f}%); the best move {best_san} gets {p_best:.0f}%."
    # --- tips
    tips = {"allowed_mate": "Before every move, list your opponent's checks and forcing replies. If any of them leads to mate, find another move.",
            "missed_mate": "Check every check first. A forcing line (check, capture, threat) is quicker to calculate than a quiet move.",
            "hung_piece": "Blunder check: after choosing a move, look at the square you are moving to and ask 'can anything capture it, and is it defended?'",
            "left_hanging": "Before moving, ask what your move stops protecting. Moving a defender away often leaves another piece hanging.",
            "missed_capture": "Scan for free material each move: look at every undefended opponent piece before you play a quiet move.",
            "missed_tactic": "Look at checks, captures and threats (for both sides) before any quiet move.",
            "eval": "Compare your move with your opponent's strongest reply. If their best answer hurts you, look for a safer alternative."}
    tl = [tips[kind]]
    clk = r["clk"]
    if clk is not None and r["timeClass"] != "daily" and clk <= 60:
        tl.append(f"You had {clk:.0f}s left on the clock. Under a minute, prefer simple, safe moves and keep a few seconds in hand for the blunder check.")
    if movenum <= 12:
        tl.append(f"This happened early (move {movenum}) in the {r['family']}. Review the main line of this opening so you recognise the position.")
    elif sb.score(mate_score=10000) >= 300:
        tl.append("You were clearly winning here. When ahead, trade pieces and avoid complications; do not give the opponent counterplay.")
    if b.piece_map() and len(b.piece_map()) <= 10 and movenum > 25:
        tl.append("In endgames, calculate concrete lines (king and pawn races, opposition) rather than playing by feel.")
    tags = tags_for(b, mv, before, after, mover)   # shared with tag_blunders.py
    return {"kind": kind, "tags": tags, "accept": accept, "headline": why[0], "why": why[1:], "better": best_san, "better_line": best_line,
            "maia": {"level": MAIA_LEVEL, "top": [[s, round(p)] for s, p in topsan], "played_pct": round(p_played), "best_pct": round(p_best), "text": mtxt},
            "tips": tl, "eval_before": fmt_cp(sb), "eval_after": fmt_cp(sa)}

done = skipped = 0
for r in rows:
    if done >= a.top: break
    n = note_for(r)
    if n is None: skipped += 1; continue
    db.execute("insert or replace into coach_notes values (?,?,?)", (r["game_id"], r["ply"], json.dumps(n, ensure_ascii=False)))
    done += 1
    ctx.progress("coach", done, a.top, f"Coach note {done} of {a.top}")
db.commit(); eng.quit(); send("quit")
ctx.progress("coach", done, a.top, f"{done} coach notes written; {skipped} candidates dropped (not blunders at depth {a.depth})")
