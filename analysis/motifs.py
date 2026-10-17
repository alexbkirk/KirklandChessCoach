"""Name the tactic in an engine line: fork, pin, skewer, discovered attack, back-rank mate and so on.

detect(board, line, attacker, me) looks at `line` (engine moves, starting with `attacker`'s key move from `board`)
and returns motif dicts {"k": key, "l": label, "d": description}, most important first. Descriptions say "your"
for `me`'s pieces and "their" for the other side's. See docs/COACHING.md for the rules.
"""
import chess

from tagging import VAL, NAME

MIN_GAIN = 2       # points of material a non-mating, non-checking motif must win
LINE_PLIES = 6     # how much of the line counts towards the material gain
ORDER = ["back_rank_mate", "smothered_mate", "mate", "double_check", "discovered_check", "fork", "discovered_attack",
         "pin", "skewer", "removing_the_defender", "moved_defender", "hung_piece", "ignored_threat", "lost_piece", "free_piece",
         "promotion"]
LABEL = {"back_rank_mate": "Back-rank mate", "smothered_mate": "Smothered mate", "mate": "Mating attack",
         "double_check": "Double check", "discovered_check": "Discovered check", "discovered_attack": "Discovered attack",
         "fork": "Fork", "pin": "Pin", "skewer": "Skewer", "removing_the_defender": "Removing the defender",
         "moved_defender": "Moved defender", "hung_piece": "Hung piece", "ignored_threat": "Ignored threat",
         "lost_piece": "Lost piece", "free_piece": "Free piece", "promotion": "Promotion"}
DIRECTIONS = {chess.ROOK: [(1, 0), (-1, 0), (0, 1), (0, -1)], chess.BISHOP: [(1, 1), (1, -1), (-1, 1), (-1, -1)]}
DIRECTIONS[chess.QUEEN] = DIRECTIONS[chess.ROOK] + DIRECTIONS[chess.BISHOP]


def _whose(color, me):
    return "your" if color == me else "their"


def _piece(board, sq, me):
    p = board.piece_at(sq)
    return f"{_whose(p.color, me)} {NAME[p.piece_type]} on {chess.square_name(sq)}"


def _material(board, color):
    return sum(VAL[p.piece_type] * (1 if p.color == color else -1) for p in board.piece_map().values() if p.piece_type != chess.KING)


def material_gain(board, line, attacker, plies=LINE_PLIES):
    """Material `attacker` wins over the first `plies` moves of `line`."""
    b = board.copy()
    for mv in line[:plies]:
        b.push(mv)
    return _material(b, attacker) - _material(board, attacker)


def _ray(board, sq, df, dr):
    """The first two occupied squares from `sq` in direction (df, dr)."""
    f, r = chess.square_file(sq) + df, chess.square_rank(sq) + dr
    found = []
    while 0 <= f < 8 and 0 <= r < 8 and len(found) < 2:
        s = chess.square(f, r)
        if board.piece_at(s):
            found.append(s)
        f, r = f + df, r + dr
    return found


def _is_target(board, sq, attacker_value):
    """Worth attacking: the king, a piece worth more than the attacker, or an undefended piece worth 3+."""
    p = board.piece_at(sq)
    if p.piece_type == chess.KING:
        return True
    if VAL[p.piece_type] > attacker_value:
        return True
    return VAL[p.piece_type] >= 3 and not board.attackers(p.color, sq)


def _san_line(board, line, n):
    b = board.copy(); out = []
    for mv in line[:n]:
        out.append(b.san(mv)); b.push(mv)
    return " ".join(out)


def _mate(board, line, attacker, me):
    """Mate patterns, if the line ends in mate by `attacker`."""
    b = board.copy()
    for i, mv in enumerate(line):
        b.push(mv)
        if b.is_checkmate():
            break
    else:
        return []
    if b.turn == attacker:   # the attacker was mated, not the defender
        return []
    n = i // 2 + 1
    king = b.king(not attacker)
    checkers = list(b.checkers())
    back = 7 if attacker == chess.WHITE else 0   # the defender's back rank
    line_txt = _san_line(board, line, i + 1)
    ring = [s for s in chess.SquareSet(chess.BB_KING_ATTACKS[king])]
    own = lambda s: b.piece_at(s) is not None and b.piece_at(s).color != attacker
    if len(checkers) == 1:
        c = checkers[0]; cp = b.piece_at(c)
        if cp.piece_type == chess.KNIGHT and all(own(s) for s in ring):
            return [{"k": "smothered_mate", "d": f"{line_txt} is a smothered mate: the king is boxed in by its own pieces"}]
        if cp.piece_type in (chess.ROOK, chess.QUEEN) and chess.square_rank(king) == back and chess.square_rank(c) == back:
            forward = [s for s in ring if chess.square_rank(s) != back]
            if forward and all(own(s) for s in forward):
                return [{"k": "back_rank_mate", "d": f"{line_txt} mates on the back rank: the king's own pawns block its escape"}]
    return [{"k": "mate", "d": f"{line_txt} is mate" if n == 1 else f"{line_txt} forces mate in {n}"}]


def detect(board, line, attacker, me):
    """Motifs in `line` (starting with `attacker`'s key move from `board`), most important first."""
    if not line:
        return []
    key = line[0]
    if key not in board.legal_moves:
        return []
    found = _mate(board, line, attacker, me)
    b0 = board; b1 = board.copy(); b1.push(key)
    san = b0.san(key)
    gain = material_gain(board, line, attacker)
    wins = gain >= MIN_GAIN
    to = key.to_square
    mover = b1.piece_at(to)
    mover_value = VAL[mover.piece_type]

    if len(b1.checkers()) >= 2:
        found.append({"k": "double_check", "d": f"{san} gives check with two pieces at once"})

    # discovered attack: another slider now sees the king or a valuable piece
    for s in b1.pieces(chess.BISHOP, attacker) | b1.pieces(chess.ROOK, attacker) | b1.pieces(chess.QUEEN, attacker):
        if s == to:
            continue
        new = b1.attacks(s) & ~b0.attacks(s) & b1.occupied_co[not attacker]
        targets = [t for t in new if _is_target(b1, t, VAL[b1.piece_at(s).piece_type])]
        if not targets:
            continue
        if any(b1.piece_at(t).piece_type == chess.KING for t in targets):
            found.append({"k": "discovered_check", "d": f"{san} uncovers check from {_piece(b1, s, me)}"})
        elif wins:
            found.append({"k": "discovered_attack", "d": f"{san} uncovers {_piece(b1, s, me)}, which hits {_piece(b1, targets[0], me)}"})
        break

    # fork: the moved piece hits two targets
    targets = [t for t in b1.attacks(to) & b1.occupied_co[not attacker] if _is_target(b1, t, mover_value)]
    if len(targets) >= 2 and (wins or found and found[0]["k"] in ("mate", "back_rank_mate", "smothered_mate")):
        targets.sort(key=lambda t: -VAL[b1.piece_at(t).piece_type])
        found.append({"k": "fork", "l": f"{NAME[mover.piece_type].capitalize()} fork",
                      "d": f"{san} hits {_piece(b1, targets[0], me)} and {_piece(b1, targets[1], me)}"})

    # pin and skewer along the moved slider's lines
    if mover.piece_type in DIRECTIONS and wins:
        for df, dr in DIRECTIONS[mover.piece_type]:
            ray = _ray(b1, to, df, dr)
            if len(ray) < 2 or any(b1.piece_at(s).color == attacker for s in ray):
                continue
            front, back = (b1.piece_at(s) for s in ray)
            if front.piece_type != chess.KING and (back.piece_type == chess.KING or VAL[back.piece_type] > VAL[front.piece_type]):
                found.append({"k": "pin", "d": f"{san} pins {_piece(b1, ray[0], me)} to {_piece(b1, ray[1], me)}"})
                break
            if back.piece_type != chess.KING and VAL[back.piece_type] >= 3 and (front.piece_type == chess.KING or VAL[front.piece_type] > VAL[back.piece_type]):
                found.append({"k": "skewer", "d": f"{san} skewers {_piece(b1, ray[0], me)}; once it moves, {_piece(b1, ray[1], me)} falls"})
                break
    # attacking a piece that is already pinned
    if wins and not any(m["k"] == "pin" for m in found):
        for t in b1.attacks(to) & b1.occupied_co[not attacker]:
            if b1.is_pinned(not attacker, t) and b1.piece_at(t).piece_type != chess.KING and VAL[b1.piece_at(t).piece_type] > mover_value:
                found.append({"k": "pin", "d": f"{san} attacks {_piece(b1, t, me)}, which is pinned and cannot move away"})
                break

    # removing the defender: capture a guard, then take what it guarded
    taken = b0.piece_at(to)
    if taken is not None and taken.color != attacker and wins:
        guarded = b0.attacks(to) & b0.occupied_co[not attacker]
        later = {mv.to_square for i, mv in enumerate(line[2:LINE_PLIES], 2) if i % 2 == 0}
        hit = [s for s in guarded if s in later]
        if hit:
            found.append({"k": "removing_the_defender", "d": f"{san} removes {_piece(b0, to, me)}, the guard of {_piece(b0, hit[0], me)}"})
        elif not b0.attackers(not attacker, to) and VAL[taken.piece_type] >= 3:
            found.append({"k": "free_piece", "d": f"{san} takes {_piece(b0, to, me)}, which nothing defended"})

    for i, mv in enumerate(line[:LINE_PLIES]):
        if i % 2 == 0 and mv.promotion:
            found.append({"k": "promotion", "d": f"{_san_line(board, line, i + 1)} promotes a pawn"})
            break

    seen = set(); out = []
    for m in sorted(found, key=lambda m: ORDER.index(m["k"])):
        if m["k"] in seen:
            continue
        seen.add(m["k"]); m.setdefault("l", LABEL[m["k"]]); out.append(m)
    return out


def blunder_motifs(board, move, reply, me):
    """What `move` (by `me`, from `board`) gave away to the opponent's `reply` when that reply takes a piece worth 3+:
    the moved piece itself (hung piece), a piece it was guarding (moved defender), a piece that was already attacked
    and left there (ignored threat), or another piece the move exposed (lost piece)."""
    if reply is None:
        return []
    after = board.copy(); after.push(move)
    if reply not in after.legal_moves or not after.is_capture(reply) or after.is_en_passant(reply):
        return []
    target = reply.to_square
    victim = after.piece_at(target)
    if victim is None or victim.color != me or VAL[victim.piece_type] < 3:
        return []
    reply_san = after.san(reply)
    if target == move.to_square:
        if not after.attackers(me, target) or VAL[after.piece_at(reply.from_square).piece_type] < VAL[victim.piece_type]:
            return [{"k": "hung_piece", "l": LABEL["hung_piece"],
                     "d": f"{reply_san} takes {_piece(after, target, me)}: it was not safe there"}]
        return []
    if target in board.attacks(move.from_square) and target not in after.attacks(move.to_square):
        p = board.piece_at(move.from_square)
        return [{"k": "moved_defender", "l": LABEL["moved_defender"],
                 "d": f"your {NAME[p.piece_type]} on {chess.square_name(move.from_square)} was guarding {_piece(after, target, me)}; once it moved, {reply_san} took it"}]
    taker = after.piece_at(reply.from_square)
    if not _loses(after, target, taker, me):
        return []
    if board.piece_at(reply.from_square) == taker and target in board.attacks(reply.from_square) and _loses(board, target, taker, me):
        return [{"k": "ignored_threat", "l": LABEL["ignored_threat"],
                 "d": f"{_piece(after, target, me)} was already attacked by their {NAME[taker.piece_type]} on {chess.square_name(reply.from_square)}, and {reply_san} takes it"}]
    return [{"k": "lost_piece", "l": LABEL["lost_piece"], "d": f"{reply_san} takes {_piece(after, target, me)}"}]


def _loses(board, sq, taker, me):
    """Taking the piece on `sq` with `taker` wins material: it is undefended, or the taker is worth less."""
    return not board.attackers(me, sq) or VAL[taker.piece_type] < VAL[board.piece_at(sq).piece_type]
