// Thin bridge between Blazor (C#) and chessground. C# sends FEN and destinations; chessground reports moves back.
// chessground does not validate moves, so legal destinations come from chess.js (passed in as a dests map).
import { Chessground } from "../lib/chessground/chessground.min.js";

export function create(element, dotNetRef, options) {
    const ground = Chessground(element, {
        fen: options.fen,
        orientation: options.orientation,
        coordinates: true,
        movable: {
            free: false,
            color: options.movableColor,
            dests: new Map(Object.entries(options.dests ?? {})),
            events: {
                after: (orig, dest) => dotNetRef.invokeMethodAsync("OnMove", orig + dest),
            },
        },
        drawable: { enabled: true },
    });
    return {
        setPosition(fen, dests, movableColor, orientation) {
            ground.set({
                fen,
                orientation,
                turnColor: movableColor,
                movable: { color: movableColor, dests: new Map(Object.entries(dests ?? {})) },
            });
        },
        setShapes(shapes) {
            ground.setShapes(shapes ?? []);
        },
        destroy() {
            ground.destroy();
        },
    };
}
