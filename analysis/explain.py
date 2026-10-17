"""Explain one blunder in words: what it allowed or missed (named tactic), whether the threat was already there,
winning chances, and time spent. Shared by coach.py (puzzle notes) and explain_blunders.py (drill notes).
See docs/COACHING.md for the rules.
"""
import math
import statistics
from collections import defaultdict

import chess
import chess.engine

from motifs import blunder_motifs, detect

PHRASE = {"back_rank_mate": "a back-rank mate", "smothered_mate": "a smothered mate", "mate": "a forced mate",
          "double_check": "a double check", "discovered_check": "a discovered check", "discovered_attack": "a discovered attack",
          "pin": "a pin", "skewer": "a skewer", "removing_the_defender": "a capture of the defender",
          "free_piece": "a free piece", "promotion": "a pawn promotion"}
MATES = ("back_rank_mate", "smothered_mate", "mate")
QUICK = chess.engine.Limit(depth=12)


def chances(cp):
    """Winning chances (expected score, 0-100) for a centipawn score from the mover's side; mates are +/-10000."""
    return 50 + 50 * (2 / (1 + math.exp(-0.00368208 * cp)) - 1)


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


# --- time use ---------------------------------------------------------------------------------------
def _increment(tc):
    return int(tc.split("+")[1]) if tc and "+" in tc else 0


def _base_time(tc):
    try:
        return int(tc.split("+")[0])
    except (AttributeError, ValueError):
        return None


def stage(movenum):
    return "opening" if movenum <= 12 else "middlegame" if movenum <= 30 else "late middlegame and endgame"


def load_time_usage(db):
    """Seconds spent on each of your moves {(game_id, ply): s}, and your median per stage {stage: s}. Skips daily games."""
    spent, per_stage, prev = {}, defaultdict(list), {}
    for game_id, ply, clk, tc in db.execute("""select m.game_id, m.ply, m.clk, g.timeControl from moves m join games g on g.id = m.game_id
                                               where m.is_mine = 1 and g.timeClass != 'daily' order by m.game_id, m.ply"""):
        last = prev.get(game_id, _base_time(tc))
        if clk is not None and last is not None:
            s = max(0.0, last - clk + _increment(tc))
            spent[(game_id, ply)] = s
            per_stage[stage((ply + 1) // 2)].append(s)
        prev[game_id] = clk
    return spent, {k: statistics.median(v) for k, v in per_stage.items() if v}


def time_fact(spent, usual, game_id, ply, san, clk):
    """'You spent 6s on Bb4; your usual in the middlegame is 10s.' or None when the clock is unknown."""
    s = spent.get((game_id, ply))
    if s is None:
        return None
    st = stage((ply + 1) // 2)
    t = f"You played {san} instantly" if s < 1 else f"You spent {s:.0f}s on {san}"
    if usual.get(st): t += f"; your usual in the {st} is {usual[st]:.0f}s"
    if clk is not None and clk < 60: t += f", and you had {clk:.0f}s left"
    return t + "."


# --- the explanation --------------------------------------------------------------------------------
def threat_was_there(eng, b, reply, theirs_after):
    """Whether the opponent's shot existed before the blunder: let them move first (a null move) and compare."""
    if reply is None or b.is_check():
        return None
    nb = b.copy(); nb.push(chess.Move.null())
    info = eng.analyse(nb, QUICK)
    theirs_before = chances(info["score"].pov(nb.turn).score(mate_score=10000))
    if theirs_before >= 60 and theirs_before >= theirs_after - 10:
        return True
    if theirs_before < 50:
        return False
    return None


def explain(eng, b, mv, before, after):
    """b: board before your move mv; before/after: engine infos (with "pv") for the positions before and after it.

    Returns side ("allowed", "missed" or "positional"), the leading motif, all motifs, a headline, "why" lines,
    the better move and line, evaluations and winning chances.
    """
    me = b.turn; san = b.san(mv)
    best = before["pv"][0]; best_san = b.san(best)
    sb, sa = before["score"].pov(me), after["score"].pov(me)
    ch_before, ch_after = chances(sb.score(mate_score=10000)), chances(sa.score(mate_score=10000))
    after_b = b.copy(); after_b.push(mv)
    reply = (after.get("pv") or [None])[0]
    gave = blunder_motifs(b, mv, reply, me)
    shot = detect(after_b, list(after.get("pv") or []), not me, me)
    mates = [x for x in shot if x["k"] in MATES]
    allowed = mates + gave + [x for x in shot if x not in mates] if mates else gave + shot
    missed = detect(b, list(before["pv"]), me, me) if best != mv else []
    reply_san = after_b.san(reply) if reply else "their reply"

    why = []
    if allowed:
        m = allowed[0]; side = "allowed"
        if m["k"] == "hung_piece": headline = f"{san} hangs a piece. {cap(m['d'])}."
        elif m["k"] == "moved_defender": headline = f"{san} pulls a defender away. {cap(m['d'])}."
        elif m["k"] == "free_piece": headline = f"{san} leaves a piece hanging. {cap(m['d'])}."
        elif m["k"] == "ignored_threat": headline = f"{san} ignores a threat. {cap(m['d'])}."
        elif m["k"] == "lost_piece": headline = f"{san} loses a piece. {cap(m['d'])}."
        else: headline = f"{san} allows {phrase(m)}. {cap(m['d'])}."
        if mates and gave: why.append(f"The cause: {gave[0]['d']}.")
        if missed: why.append(f"You also missed {phrase(missed[0])} yourself: {missed[0]['d']}.")
    elif missed and ch_after >= 40:   # the cost was the chance you missed, not a shot you allowed
        m = missed[0]; side = "missed"
        headline = f"You missed {phrase(m)}. {cap(m['d'])}; you played {san} instead."
    else:
        m = None; side = "positional"
        if ch_before >= 50 and ch_after < 30:
            headline = f"{san} hands over the game: after {reply_san} they are clearly better, and {best_san} was the move."
        else:
            headline = f"{san} costs you: {reply_san} follows, and {best_san} was the better move."
        if missed: why.append(f"You also missed {phrase(missed[0])}: {missed[0]['d']}.")
    # A hung piece is a threat the move itself created, and an ignored threat says it was already there: skip both.
    if side == "positional" or (side == "allowed" and m["k"] not in ("hung_piece", "ignored_threat")):
        there = threat_was_there(eng, b, reply, 100 - ch_after)
        if there is True: why.append(f"The threat was already on the board before {san}; your move did nothing about it.")
        elif there is False and side == "allowed": why.append(f"Before {san} they had no such shot. Your move created it.")
        elif there is False: why.append(f"Before {san} they had nothing. Your move created the problem.")
    why.append(f"Winning chances: {ch_before:.0f}% before, {ch_after:.0f}% after.")
    return {"side": side, "motif": m, "motifs": {"allowed": allowed, "missed": missed}, "headline": headline, "why": why,
            "better": best_san, "better_line": line_san(b, before["pv"], 4), "eval_before": fmt_cp(sb), "eval_after": fmt_cp(sa),
            "swing": round(ch_before - ch_after, 1)}
