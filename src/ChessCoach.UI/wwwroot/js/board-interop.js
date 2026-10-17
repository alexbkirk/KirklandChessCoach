// Bridge between Blazor (C#) and chessground. C# deals only in FEN, square names and UCI strings.
// Legal moves come from chess.js, so chessground only lets the user play legal moves.
import { Chessground } from "../lib/chessground/chessground.min.js";
import { Chess } from "../lib/chessjs/chess.mjs";

const LAST_RANK = { w: "8", b: "1" };

/** Destinations for the side to move, as chessground expects them: { "e2": ["e3", "e4"], ... }. */
function destinations(fen, movableColor) {
    const dests = {};
    if (!movableColor) return dests;
    const chess = new Chess(fen);
    if ((movableColor === "white" ? "w" : "b") !== chess.turn()) return dests;
    for (const move of chess.moves({ verbose: true })) {
        (dests[move.from] ??= []).push(move.to);
    }
    return dests;
}

/** The move on the board as { uci, san }. Pawns reaching the last rank are promoted to a queen (the only choice offered). */
function moveFor(fen, orig, dest) {
    const chess = new Chess(fen);
    const piece = chess.get(orig);
    const promotion = piece && piece.type === "p" && dest[1] === LAST_RANK[piece.color] ? "q" : undefined;
    const move = chess.move({ from: orig, to: dest, promotion });
    return { uci: orig + dest + (promotion ?? ""), san: move.san };
}

function turnColor(fen) {
    return fen.split(" ")[1] === "b" ? "black" : "white";
}

/** chessground config for the options C# sends: fen, orientation, movableColor, lastMove (uci), marks. */
function configFor(o) {
    return {
        fen: o.fen,
        orientation: o.orientation,
        turnColor: turnColor(o.fen),
        lastMove: o.lastMove ? [o.lastMove.slice(0, 2), o.lastMove.slice(2, 4)] : undefined,
        movable: {
            color: o.movableColor ?? undefined,
            dests: new Map(Object.entries(destinations(o.fen, o.movableColor))),
        },
    };
}

/** Circles on squares, or arrows when a mark has a `to` square. Brushes: green, red, blue, yellow. */
function shapesFor(marks) {
    return (marks ?? []).map(m => (m.to ? { orig: m.square, dest: m.to, brush: m.brush } : { orig: m.square, brush: m.brush }));
}

export function create(element, dotNetRef, options) {
    let current = options;
    const ground = Chessground(element, {
        ...configFor(current),
        coordinates: true,
        movable: {
            ...configFor(current).movable,
            free: false,
            events: {
                after: (orig, dest) => {
                    const { uci, san } = moveFor(current.fen, orig, dest);
                    return dotNetRef.invokeMethodAsync("OnMove", uci, san);
                },
            },
        },
        events: {
            select: key => {
                if (current.selectable) dotNetRef.invokeMethodAsync("OnSquare", key);
            },
        },
        drawable: { enabled: false },
    });
    ground.setAutoShapes(shapesFor(current.marks));
    return {
        update(options) {
            current = options;
            ground.set(configFor(current));
            ground.selectSquare(null);
            ground.setAutoShapes(shapesFor(current.marks));
        },
        destroy() {
            ground.destroy();
        },
    };
}
