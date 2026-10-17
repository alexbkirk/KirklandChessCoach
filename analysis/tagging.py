"""Blunder tags, shared by coach.py (depth 16, top 100) and tag_blunders.py (depth 12, every blunder).

A blunder can carry several tags:
  allowed_mate   your move let the opponent force mate
  missed_mate    you had a forced mate and did not play it
  one_move       you lose >= MAT_THRESHOLD points of material within the opponent's next two moves
  missed_capture the best move won a free piece (>= 3 points) and you did not capture it
  missed_tactic  the best move was a check/mate or won material through a short line
  other          none of the above (a positional slip)
"""
import chess

VAL = {chess.PAWN: 1, chess.KNIGHT: 3, chess.BISHOP: 3, chess.ROOK: 5, chess.QUEEN: 9, chess.KING: 100}
NAME = {chess.PAWN: "pawn", chess.KNIGHT: "knight", chess.BISHOP: "bishop", chess.ROOK: "rook", chess.QUEEN: "queen", chess.KING: "king"}
MAT_THRESHOLD = 3   # points of net material (a minor piece)


def material_lost(board, moves, mover, plies=3):
    """Net material (pawn=1 ... queen=9) the mover is down after playing `moves[:plies]` from `board`, vs now."""
    def diff(b): return sum(VAL[p.piece_type] * (1 if p.color == mover else -1) for p in b.piece_map().values() if p.piece_type != chess.KING)
    b = board.copy(); d0 = diff(b)
    for m in moves[:plies]: b.push(m)
    return d0 - diff(b)


def tags_for(b, mv, before, after, mover):
    """b: board before the blunder; mv: the move played; before/after: engine infos for the position before / after it."""
    sb, sa = before["score"].pov(mover), after["score"].pov(mover)
    best = before["pv"][0]; best_san = b.san(best)
    b2 = b.copy(); b2.push(best); best_check, best_mate = b2.is_check(), b2.is_checkmate()
    best_cap = b.piece_at(best.to_square) if b.is_capture(best) and not b.is_en_passant(best) else None
    allowed = sa.is_mate() and sa.mate() < 0
    missed = (not allowed) and sb.is_mate() and sb.mate() > 0 and not (sa.is_mate() and sa.mate() > 0)
    tags = []
    if allowed: tags.append({"k": "allowed_mate", "l": f"Allowed mate in {abs(sa.mate())}"})
    if missed: tags.append({"k": "missed_mate", "l": f"Missed mate in {sb.mate()}"})
    if not allowed:
        lost = material_lost(b, [mv] + list(after.get("pv") or [])[:2], mover)   # my move, their reply, my move
        if lost >= MAT_THRESHOLD: tags.append({"k": "one_move", "l": "One-move blunder", "d": f"Loses {lost} points of material within the opponent's next two moves"})
    if not missed:
        gain = -material_lost(b, list(before["pv"])[:3], mover)   # best move, their reply, my move
        if gain >= MAT_THRESHOLD or best_mate or best_check:
            if best_cap and VAL[best_cap.piece_type] >= 3 and not b.is_capture(mv):
                tags.append({"k": "missed_capture", "l": "Missed capture", "d": f"{best_san} wins a free {NAME[best_cap.piece_type]}"})
            else:
                tags.append({"k": "missed_tactic", "l": "Missed tactic", "d": f"{best_san} {'mates' if best_mate else 'checks' if best_check else 'wins material'}"})
    if not tags: tags.append({"k": "other", "l": "Positional slip", "d": "No immediate material or mate consequence; the position simply got worse"})
    return tags


def hanging_squares(board, color):
    """Squares of `color` pieces (not the king) that are attacked and either undefended or attacked by a cheaper piece."""
    out = []
    for sq, p in board.piece_map().items():
        if p.color != color or p.piece_type == chess.KING: continue
        att = board.attackers(not color, sq)
        if att and (not board.attackers(color, sq) or min(VAL[board.piece_at(x).piece_type] for x in att) < VAL[p.piece_type]):
            out.append(chess.square_name(sq))
    return sorted(out)
