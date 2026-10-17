"""Generate coach notes for the blunders that cost the most winning chances (Stockfish + Maia 1100), stored in table `coach_notes`.

Selection and note contents are described in docs/COACHING.md.
Usage: python -I coach.py --data-dir <folder> [--top 100] [--depth 16] [--progress jsonl]
"""
import argparse, json, math, os, re, sqlite3, statistics, subprocess, sys
from collections import Counter, defaultdict
import chess, chess.engine

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from common import Context, add_common_args
from motifs import LABEL, blunder_motifs, detect
from tagging import tags_for

MAIA_LEVEL = 1100
MIN_CHANCES_BEFORE = 30   # skip positions that were already lost
MIN_DROP = 25             # winning-chance points lost, at the analysis depth
MIN_DROP_RECHECK = 20     # and again at --depth
PATTERN_MIN = 3
PHRASE = {"back_rank_mate": "a back-rank mate", "smothered_mate": "a smothered mate", "mate": "a forced mate",
          "double_check": "a double check", "discovered_check": "a discovered check", "discovered_attack": "a discovered attack",
          "pin": "a pin", "skewer": "a skewer", "removing_the_defender": "a capture of the defender",
          "free_piece": "a free piece", "promotion": "a pawn promotion"}
PLURAL = {"Removing the defender": "Captures of the defender", "Free piece": "Free pieces", "Hung piece": "Hung pieces",
          "Moved defender": "Moved defenders", "Double check": "Double checks", "Promotion": "Promotions"}

ap = argparse.ArgumentParser()
add_common_args(ap)
ap.add_argument("--top", type=int, default=100)
ap.add_argument("--depth", type=int, default=16)
a = ap.parse_args()
ctx = Context(a)
SF, LC0, MAIA = ctx.stockfish, ctx.lc0, ctx.maia


def chances(cp):
    """Winning chances (expected score, 0-100) for a centipawn score from the mover's side; mates are +/-10000."""
    return 50 + 50 * (2 / (1 + math.exp(-0.00368208 * cp)) - 1)


def increment(tc):
    return int(tc.split("+")[1]) if tc and "+" in tc else 0


def base_time(tc):
    try:
        return int(tc.split("+")[0])
    except (AttributeError, ValueError):
        return None


def stage(movenum):
    return "opening" if movenum <= 12 else "middlegame" if movenum <= 30 else "late middlegame and endgame"


db = ctx.connect(); db.row_factory = sqlite3.Row

# --- time spent on each of your moves, and your usual time per stage ------------------------------
spent, usual = {}, defaultdict(list)
prev = {}
for r in db.execute("""select m.game_id, m.ply, m.clk, g.timeControl from moves m join games g on g.id = m.game_id
                       where m.is_mine = 1 and g.timeClass != 'daily' order by m.game_id, m.ply"""):
    last = prev.get(r["game_id"], base_time(r["timeControl"]))
    if r["clk"] is not None and last is not None:
        s = max(0.0, last - r["clk"] + increment(r["timeControl"]))
        spent[(r["game_id"], r["ply"])] = s
        usual[stage((r["ply"] + 1) // 2)].append(s)
    prev[r["game_id"]] = r["clk"]
usual = {k: statistics.median(v) for k, v in usual.items() if v}

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
quick = chess.engine.Limit(depth=12)


def fmt_cp(score):
    if score.is_mate(): return f"mate in {abs(score.mate())}" if score.mate() > 0 else f"getting mated in {abs(score.mate())}"
    return f"{score.score() / 100:+.1f}"


def line_san(board, pv, n):
    b = board.copy(); out = []
    for mv in pv[:n]:
        out.append(b.san(mv)); b.push(mv)
    return " ".join(out)


def cap(text):
    return text[0].upper() + text[1:]


def phrase(m):
    if m["k"] == "fork": return f"a {m['l'].lower()}"
    return PHRASE.get(m["k"], m["l"].lower())


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


def threat_was_there(b, reply, theirs_after):
    """Whether the opponent's shot existed before the blunder: let them move first (a null move) and compare."""
    if reply is None or b.is_check():
        return None
    nb = b.copy(); nb.push(chess.Move.null())
    info = eng.analyse(nb, quick)
    theirs_before = chances(info["score"].pov(nb.turn).score(mate_score=10000))
    if theirs_before >= 60 and theirs_before >= theirs_after - 10:
        return True
    if theirs_before < 50:
        return False
    return None


def note_for(r):
    b = chess.Board(r["fen_before"]); me = b.turn; mv = chess.Move.from_uci(r["uci"])
    san = r["san"]; movenum = (r["ply"] + 1) // 2
    multi = eng.analyse(b, lim, multipv=5)
    before = multi[0]
    best = before["pv"][0]; best_san = b.san(best); sb = before["score"].pov(me)
    # puzzle answers: every move within 50 centipawns of the best (mate scores compared as +/-10000)
    top = sb.score(mate_score=10000)
    accept = [i["pv"][0].uci() for i in multi if i.get("pv") and i["score"].pov(me).score(mate_score=10000) >= top - 50]
    if best.uci() not in accept: accept.insert(0, best.uci())
    after_b = b.copy(); after_b.push(mv)
    after = eng.analyse(after_b, lim); sa = after["score"].pov(me)
    ch_before, ch_after = chances(sb.score(mate_score=10000)), chances(sa.score(mate_score=10000))
    if ch_before - ch_after < MIN_DROP_RECHECK: return None   # not costly at the higher depth
    reply = (after.get("pv") or [None])[0]
    gave = blunder_motifs(b, mv, reply, me)
    shot = detect(after_b, list(after.get("pv") or []), not me, me)
    mates = [x for x in shot if x["k"] in ("back_rank_mate", "smothered_mate", "mate")]
    allowed = mates + gave + [x for x in shot if x not in mates] if mates else gave + shot
    missed = detect(b, list(before["pv"]), me, me) if best != mv else []
    reply_san = after_b.san(reply) if reply else "their reply"

    why = []
    if allowed:
        m = allowed[0]; side = "allowed"
        if m["k"] == "hung_piece": headline = f"{san} hangs a piece. {cap(m['d'])}."
        elif m["k"] == "moved_defender": headline = f"{san} pulls a defender away. {cap(m['d'])}."
        elif m["k"] == "free_piece": headline = f"{san} leaves a piece hanging. {cap(m['d'])}."
        else: headline = f"{san} allows {phrase(m)}. {cap(m['d'])}."
        if mates and gave: why.append(f"The cause: {gave[0]['d']}.")
        if missed: why.append(f"You also missed {phrase(missed[0])} yourself: {missed[0]['d']}.")
    elif missed and ch_after >= 40:   # the cost was the chance you missed, not a shot you allowed
        m = missed[0]; side = "missed"
        headline = f"You missed {phrase(m)}. {cap(m['d'])}; you played {san} instead."
    else:
        m = None; side = "positional"
        headline = f"{san} hands over the game: after {reply_san} they are clearly better, and {best_san} was the move."
        if missed: why.append(f"You also missed {phrase(missed[0])}: {missed[0]['d']}.")
    if side == "allowed" or side == "positional":
        there = threat_was_there(b, reply, 100 - ch_after)
        if there is True: why.append(f"The threat was already on the board before {san}; your move did nothing about it.")
        elif there is False and side == "allowed": why.append(f"Before {san} they had no such shot. Your move created it.")
        elif there is False: why.append(f"Before {san} they had nothing. Your move created the problem.")
    why.append(f"Winning chances: {ch_before:.0f}% before, {ch_after:.0f}% after.")
    s = spent.get((r["game_id"], r["ply"]))
    if s is not None:
        norm = usual.get(stage(movenum))
        t = f"You played {san} instantly" if s < 1 else f"You spent {s:.0f}s on {san}"
        if norm: t += f"; your usual in the {stage(movenum)} is {norm:.0f}s"
        if r["clk"] is not None and r["clk"] < 60: t += f", and you had {r['clk']:.0f}s left"
        why.append(t + ".")
    probs = maia_policy(r["fen_before"])
    top3 = sorted(probs.items(), key=lambda kv: -kv[1])[:3]
    return {"kind": f"{side}:{m['k']}" if m else side, "side": side, "motif": m,
            "motifs": {"allowed": allowed, "missed": missed}, "tags": tags_for(b, mv, before, after, me),
            "accept": accept, "headline": headline, "why": why, "better": best_san, "better_line": line_san(b, before["pv"], 4),
            "maia": {"level": MAIA_LEVEL, "top": [[b.san(chess.Move.from_uci(u)), round(p)] for u, p in top3],
                     "played_pct": round(probs.get(r["uci"], 0.0)), "best_pct": round(probs.get(best.uci(), 0.0)),
                     "text": maia_text(probs, b, san, r["uci"], best, best_san)},
            "tips": [], "swing": round(ch_before - ch_after, 1), "eval_before": fmt_cp(sb), "eval_after": fmt_cp(sa)}


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
