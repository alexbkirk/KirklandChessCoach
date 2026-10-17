# Coach notes

How `analysis/coach.py` chooses blunders and explains them. The app shows each note on the puzzle card.

## Why this changed

The first version had three problems:

- **Wrong moments.** Blunders were ranked by centipawn loss, capped at 1000, and a forced mate counts as 10,000. So "mate in 2 → +8.7" ranked as a top blunder. 38 of the 100 notes were positions that were still clearly winning afterwards.
- **Canned tips.** Each note got one of seven fixed sentences chosen by blunder type, plus situational lines. "You were clearly winning, trade pieces" appeared on 81 of 100 notes.
- **No specifics.** A note said a piece was left hanging but not how: which tactic, which pieces, and whether the threat was already on the board.

## Selection

Each position is scored as **winning chances**: the expected score for the side to move, from 0 to 100%. The curve is the same one Lichess uses: `50 + 50 * (2 / (1 + exp(-0.00368208 * cp)) - 1)`, and mate counts as 0 or 100.

A candidate is one of your blunders (not the last move of the game) where:

- your winning chances before the move were at least 30%, so the game was not already lost;
- the move cost at least 25 points of winning chances, using the depth-12 evaluations in `moves`.

Candidates are ranked by that drop, largest first, then by most recent game. Each one is re-checked at depth 16 and kept only if it still costs at least 20 points. The note stores the drop as `swing`, and the app orders the puzzle pool by it.

## Tactic detection (`analysis/motifs.py`)

`detect(board, line, attacker, me)` looks at an engine line that starts with the attacker's key move. It returns the motifs found, most important first:

| key | what is checked |
|---|---|
| `back_rank_mate` | mate by a rook or queen along the back rank, with the king boxed in by its own pieces |
| `smothered_mate` | knight mate with every square around the king taken by its own pieces |
| `mate` | any other forced mate in the line |
| `double_check` | the key move gives check with two pieces |
| `discovered_check` / `discovered_attack` | the key move uncovers a rook, bishop or queen on the king or on a valuable or undefended piece |
| `fork` | the moved piece attacks two targets: the king, a piece worth more than it, or an undefended piece worth 3+ |
| `pin` | a slider attacks a piece with a more valuable piece (or the king) behind it, or the key move attacks a piece that is already pinned |
| `skewer` | a slider attacks a valuable piece (or the king) with a piece worth 3+ behind it |
| `removing_the_defender` | the key move captures a piece that was guarding something taken later in the line |
| `free_piece` | the key move takes an undefended piece |
| `promotion` | the line promotes a pawn |

Apart from mates and checks, a motif counts only if the line wins at least 2 points of material.

Two more checks use the blunder itself:

- `moved_defender`: the piece you moved was guarding the square your opponent then captures on.
- `hung_piece`: the opponent simply takes the piece you just moved.

## The note

- **Headline.** What the move allowed, named and concrete: "Bd3 allows a knight fork: Nc7+ hits your king on e8 and your rook on a8." If the move allowed nothing nameable, the headline is what you missed instead.
- **Facts**, each only when it applies:
  - whether the threat was already on the board before your move (checked with a null move at depth 12) or your move created it;
  - what you missed, if the best move was a tactic;
  - winning chances before and after;
  - how long you spent on the move, against your usual time for that stage of the game.
- **Cause.** When the move allows a mate and also pulled a defender away or hung a piece, the mate leads the note and the cause follows it.
- **Pattern.** When the same motif is behind 3 or more of the selected blunders: "Knight forks allowed: 9 of these 100 blunders."
- **Maia.** One short line saying whether players at 1100 usually play your move or find the best one.

There are no generic tips. Every line comes from the position or from your games.

## Tests

```
analysis\.venv\Scripts\python -m unittest discover -s analysis/tests
```
