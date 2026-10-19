import { test } from "node:test";
import assert from "node:assert/strict";
import { parseMoves } from "../../src/ChessCoach.UI/wwwroot/js/chess-rules.js";

test("parses a legal six-move game with move numbers", () => {
    const result = parseMoves("1. e4 e5 2. Nf3 Nc6 3. Bb5 a6 4. Ba4 Nf6 5. O-O Be7 6. Re1 b5");
    assert.deepEqual(result, {
        sans: ["e4", "e5", "Nf3", "Nc6", "Bb5", "a6", "Ba4", "Nf6", "O-O", "Be7", "Re1", "b5"],
        error: null,
    });
});

test("ignores comments, ;-comments and result markers", () => {
    const result = parseMoves("1. e4 {King's pawn} e5 2. Nf3 Nc6 ; developing\n3. Bb5 a6 1/2-1/2");
    assert.deepEqual(result, {
        sans: ["e4", "e5", "Nf3", "Nc6", "Bb5", "a6"],
        error: null,
    });
});

test("reports the first illegal move with its 0-based index", () => {
    const result = parseMoves("1. e4 e5 2. Nf3 Nc6 3. Nxe4 Nf6");
    assert.equal(result.sans.length, 4);
    assert.deepEqual(result.sans, ["e4", "e5", "Nf3", "Nc6"]);
    assert.equal(result.error.index, 4);
    assert.equal(result.error.text, "Nxe4");
    assert.equal(typeof result.error.message, "string");
});

test("empty input returns no moves and no error", () => {
    assert.deepEqual(parseMoves(""), { sans: [], error: null });
    assert.deepEqual(parseMoves("   \n  "), { sans: [], error: null });
});

test("accepts a game written across several lines", () => {
    const result = parseMoves("1. e4 e5\n2. Nf3 Nc6\n\n3.  Bb5   a6\n");
    assert.deepEqual(result, {
        sans: ["e4", "e5", "Nf3", "Nc6", "Bb5", "a6"],
        error: null,
    });
});

test("castling written with zeros is read as castling", () => {
    const result = parseMoves("1. e4 e5 2. Nf3 Nc6 3. Bc4 Bc5 4. 0-0 Nf6 5. d3 0-0");
    assert.equal(result.error, null);
    assert.deepEqual(result.sans.filter((s) => s.startsWith("O")), ["O-O", "O-O"]);
});
