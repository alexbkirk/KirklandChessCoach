// Chess rules the UI needs at runtime (the C# side has no chess library). Pure functions over FEN strings.
import { Chess } from "../lib/chessjs/chess.mjs";

const VALUE = { p: 1, n: 3, b: 3, r: 5, q: 9, k: 100 };

function load(fen) {
    const chess = new Chess();
    chess.load(fen, { skipValidation: true });
    return chess;
}

/** Same position with `color` ("w"/"b") to move; en passant is cleared so the FEN stays valid. */
function withTurn(fen, color) {
    const parts = fen.split(" ");
    parts[1] = color;
    parts[3] = "-";
    return parts.join(" ");
}

/**
 * Squares of `color`'s pieces (not the king) that the opponent can capture and that are either undefended or
 * attacked by a cheaper piece. Attackers come from the opponent's legal captures, so pinned attackers do not count.
 */
export function hangingSquares(fen, color) {
    const enemy = color === "w" ? "b" : "w";
    const cheapestAttacker = {};
    for (const move of load(withTurn(fen, enemy)).moves({ verbose: true })) {
        if (!move.captured) continue;
        cheapestAttacker[move.to] = Math.min(cheapestAttacker[move.to] ?? Infinity, VALUE[move.piece]);
    }
    const board = load(fen);
    const out = [];
    for (const row of board.board()) {
        for (const piece of row) {
            if (!piece || piece.color !== color || piece.type === "k") continue;
            const attacker = cheapestAttacker[piece.square];
            if (attacker === undefined) continue;
            if (!board.isAttacked(piece.square, color) || attacker < VALUE[piece.type]) out.push(piece.square);
        }
    }
    return out.sort();
}

const RESULT_TOKENS = new Set(["1-0", "0-1", "1/2-1/2", "*"]);

/**
 * Parses pasted or typed standard algebraic notation from the start position.
 * Move numbers, {comments}, ;comments, result markers, NAGs and extra whitespace are ignored.
 * Returns { sans, error }: sans are the normalised SAN moves played; error is null when every move is legal,
 * otherwise { index, text, message } for the first move that is not legal (index is 0-based in the move list).
 */
export function parseMoves(text) {
    const cleaned = String(text ?? "")
        .replace(/\{[^}]*\}/g, " ")
        .replace(/;[^\n]*/g, " ");
    const tokens = [];
    for (const raw of cleaned.split(/\s+/)) {
        if (!raw || RESULT_TOKENS.has(raw) || /^\$\d+$/.test(raw)) continue;
        const token = raw.replace(/^\d+\.+/, "").replace(/[!?]+$/, "").replace(/^0-0-0(?=[+#]?$)/, "O-O-O").replace(/^0-0(?=[+#]?$)/, "O-O");
        if (token && !RESULT_TOKENS.has(token)) tokens.push(token);
    }
    const chess = new Chess();
    const sans = [];
    for (let index = 0; index < tokens.length; index++) {
        const text = tokens[index];
        try {
            sans.push(chess.move(text).san);
        } catch {
            return { sans, error: { index, text, message: `Illegal move "${text}" at move ${index + 1}` } };
        }
    }
    return { sans, error: null };
}

/** The position after `uci` is played from `fen`, or null when the move is not legal there. */
export function fenAfter(fen, uci) {
    const chess = load(fen);
    try {
        chess.move({ from: uci.slice(0, 2), to: uci.slice(2, 4), promotion: uci[4] });
    } catch {
        return null;
    }
    return chess.fen();
}

/**
 * Everything the daily drill needs for one position and the move played in it:
 * checks/captures = destination squares of the mover's checking/capturing moves; threats = the mover's pieces
 * already hanging before the move; hanging = the mover's pieces hanging after it; fenAfter = position after it.
 */
export function drillAnswers(fen, uci) {
    const chess = load(fen);
    const me = chess.turn();
    const moves = chess.moves({ verbose: true });
    const unique = list => [...new Set(list)].sort();
    const after = load(fen);
    after.move({ from: uci.slice(0, 2), to: uci.slice(2, 4), promotion: uci[4] });
    const fenAfter = after.fen();
    return {
        fenAfter,
        checks: unique(moves.filter(m => /[+#]$/.test(m.san)).map(m => m.to)),
        captures: unique(moves.filter(m => m.captured).map(m => m.to)),
        threats: hangingSquares(fen, me),
        hanging: hangingSquares(fenAfter, me),
    };
}
