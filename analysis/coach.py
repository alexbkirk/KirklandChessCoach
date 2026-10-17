"""Generate coach notes for the blunders that cost the most winning chances (Stockfish + Maia 1100), stored in table `coach_notes`.

Selection and note contents are described in docs/COACHING.md.
Usage: python -I coach.py --data-dir <folder> [--top 100] [--depth 16] [--progress jsonl]
"""
import argparse, json, os, re, sqlite3, subprocess, sys
from collections import Counter
import chess, chess.engine

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from common import Context, add_common_args
from explain import chances, explain, load_time_usage, time_fact
from tagging import tags_for

MAIA_LEVEL = 1100
MIN_CHANCES_BEFORE = 30   # skip positions that were already lost
MIN_DROP = 25             # winning-chance points lost, at the analysis depth
MIN_DROP_RECHECK = 20     # and again at --depth
PATTERN_MIN = 3
PLURAL = {"Removing the defender": "Captures of the defender", "Free piece": "Free pieces", "Hung piece": "Hung pieces",
          "Moved defender": "Moved defenders", "Double check": "Double checks", "Promotion": "Promotions"}

ap = argparse.ArgumentParser()
add_common_args(ap)
ap.add_argument("--top", type=int, default=100)
ap.add_argument("--depth", type=int, default=16)
a = ap.parse_args()
ctx = Context(a)
SF, LC0, MAIA = ctx.stockfish, ctx.lc0, ctx.maia


db = ctx.connect(); db.row_factory = sqlite3.Row

spent, usual = load_time_usage(db)

# --- candidates: blunders that cost the most winning chances ---------------------------------------
candidates = []
for r in db.execute("""select m.game_id, m.ply, m.color, m.san, m.uci, m.fen_before, m.eval_before, m.eval_after, m.clk,
        g.family, g.timeClass, g.date, g.time from moves m join games g on g.id = m.game_id
      where m.is_mine = 1 and m.is_last = 0 and m.class = 'blunder' and m.eval_before is not null and m.eval_after is not null"""):
    sign = 1 if r["color"] == "white" else -1
    before, after = chances(sign * r["eval_before"]), chances(sign * r["eval_after"])
    if before >= MIN_CHANCES_BEFORE and before - after >= MIN_DROP:
        candidates.append((before - after, r))
candidates.sort(key=lambda c: (c[1]["date"], c[1]["time"]), reverse=True)
candidates.sort(key=lambda c: round(c[0]), reverse=True)   # stable sort: biggest drop first, then most recent
ctx.progress("coach", 0, a.top, f"{len(candidates)} blunders cost at least {MIN_DROP} points of winning chances")

# --- Maia: policy head only (1 node), parse per-move probabilities ---------------------------------
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


def maia_text(probs, b, san, uci, best, best_san):
    top = sorted(probs.items(), key=lambda kv: -kv[1])[:3]
    p_played, p_best = probs.get(uci, 0.0), probs.get(best.uci(), 0.0)
    if p_played >= 20:
        return f"{p_played:.0f}% of {MAIA_LEVEL}-rated players would also play {san} here: a common trap at your level."
    if top and top[0][0] == best.uci():
        return f"The best move, {best_san}, is also the most popular choice of {MAIA_LEVEL}-rated players here ({p_best:.0f}%): one you can find."
    if p_best < 10:
        return f"Only {p_best:.0f}% of {MAIA_LEVEL}-rated players find {best_san}: a hard move to see."
    return f"Most {MAIA_LEVEL}-rated players would play {b.san(chess.Move.from_uci(top[0][0]))} ({top[0][1]:.0f}%); {best_san} gets {p_best:.0f}%."


def note_for(r):
    b = chess.Board(r["fen_before"]); me = b.turn; mv = chess.Move.from_uci(r["uci"])
    multi = eng.analyse(b, lim, multipv=5)
    before = multi[0]
    best = before["pv"][0]; sb = before["score"].pov(me)
    # puzzle answers: every move within 50 centipawns of the best (mate scores compared as +/-10000)
    top = sb.score(mate_score=10000)
    accept = [i["pv"][0].uci() for i in multi if i.get("pv") and i["score"].pov(me).score(mate_score=10000) >= top - 50]
    if best.uci() not in accept: accept.insert(0, best.uci())
    after_b = b.copy(); after_b.push(mv)
    after = eng.analyse(after_b, lim)
    if chances(top) - chances(after["score"].pov(me).score(mate_score=10000)) < MIN_DROP_RECHECK:
        return None   # not costly at the higher depth
    note = explain(eng, b, mv, before, after)
    t = time_fact(spent, usual, r["game_id"], r["ply"], r["san"], r["clk"])
    if t: note["why"].append(t)
    probs = maia_policy(r["fen_before"])
    top3 = sorted(probs.items(), key=lambda kv: -kv[1])[:3]
    m = note["motif"]
    note.update({"kind": f"{note['side']}:{m['k']}" if m else note["side"], "tags": tags_for(b, mv, before, after, me), "accept": accept,
                 "maia": {"level": MAIA_LEVEL, "top": [[b.san(chess.Move.from_uci(u)), round(p)] for u, p in top3],
                          "played_pct": round(probs.get(r["uci"], 0.0)), "best_pct": round(probs.get(best.uci(), 0.0)),
                          "text": maia_text(probs, b, r["san"], r["uci"], best, b.san(best))},
                 "tips": []})
    return note


notes = []
skipped = 0
for drop, r in candidates:
    if len(notes) >= a.top: break
    n = note_for(r)
    if n is None: skipped += 1; continue
    notes.append((r, n))
    ctx.progress("coach", len(notes), a.top, f"Coach note {len(notes)} of {a.top}")

# --- patterns: the same motif behind several of these blunders --------------------------------------
def pattern_key(n):
    return (n["side"], n["motif"]["l"]) if n["motif"] else None

counts = Counter(pattern_key(n) for _, n in notes if pattern_key(n))
for _, n in notes:
    k = pattern_key(n)
    if k and counts[k] >= PATTERN_MIN:
        label = PLURAL.get(k[1], k[1] + "s")
        what = f"{label} allowed" if k[0] == "allowed" else f"Missed {label[0].lower() + label[1:]}"
        n["pattern"] = f"{what}: {counts[k]} of these {len(notes)} blunders."

db.execute("delete from coach_notes")
db.executemany("insert or replace into coach_notes values (?,?,?)",
               [(r["game_id"], r["ply"], json.dumps(n, ensure_ascii=False)) for r, n in notes])
db.commit(); eng.quit(); send("quit")
ctx.progress("coach", len(notes), a.top, f"{len(notes)} coach notes written; {skipped} candidates dropped (cost less than {MIN_DROP_RECHECK} points at depth {a.depth})")
