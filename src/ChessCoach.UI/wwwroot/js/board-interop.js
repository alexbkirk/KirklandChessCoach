// Bridge between Blazor (C#) and chessground. C# deals only in FEN and UCI strings.
// Legal moves come from chess.js, so chessground only lets the user play legal moves.
import { Chessground } from "../lib/chessground/chessground.min.js";
import { Chess } from "../lib/chessjs/chess.mjs";

const LAST_RANK = { w: "8", b: "1" };

/** Destinations for the side to move, as chessground expects them: { "e2": ["e3", "e4"], ... }. */
function destinations(fen, movableColor) {
    const chess = new Chess(fen);
    const dests = {};
    if (!movableColor || (movableColor === "white" ? "w" : "b") !== chess.turn()) return dests;
    for (const move of chess.moves({ verbose: true })) {
        (dests[move.from] ??= []).push(move.to);
    }
    return dests;
}

function colorKey(movableColor) {
    return movableColor === "white" ? "w" : movableColor === "black" ? "b" : null;
}

/** The move on the board as { uci, san }. Pawns reaching the last rank are promoted to a queen (the only choice offered). */
function moveFor(fen, orig, dest) {
    const chess = new Chess(fen);
    const piece = chess.get(orig);
    const promotion = piece && piece.type === "p" && dest[1] === LAST_RANK[piece.color] ? "q" : undefined;
    const move = chess.move({ from: orig, to: dest, promotion });
    return { uci: orig + dest + (promotion ?? ""), san: move.san };
}

export function create(element, dotNetRef, options) {
    let fen = options.fen;
    let movableColor = options.movableColor;
    const ground = Chessground(element, {
        fen,
        orientation: options.orientation,
        coordinates: true,
        turnColor: colorKey(movableColor) === "b" ? "black" : "white",
        movable: {
            free: false,
            color: movableColor,
            dests: new Map(Object.entries(destinations(fen, movableColor))),
            events: {
                after: (orig, dest) => {
                    const { uci, san } = moveFor(fen, orig, dest);
                    return dotNetRef.invokeMethodAsync("OnMove", uci, san);
                },
            },
        },
        drawable: { enabled: true },
    });
    return {
        setPosition(newFen, newMovableColor, orientation) {
            fen = newFen;
            movableColor = newMovableColor;
            ground.set({
                fen,
                orientation,
                turnColor: colorKey(movableColor) === "b" ? "black" : "white",
                movable: {
                    color: movableColor,
                    dests: new Map(Object.entries(destinations(fen, movableColor))),
                },
            });
        },
        destroy() {
            ground.destroy();
        },
    };
}
