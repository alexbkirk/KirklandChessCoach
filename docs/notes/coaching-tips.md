# To do: reword the coaching tips

The tips read awkwardly. Reword them for clarity and tone. Examples of what to look at:

- Generic tips chosen by blunder type (`analysis/coach.py`, `tips` dict). Several sound like rules copied from a textbook rather than advice for this player.
- Situational tips appended per blunder: the "early in the opening" line, the "clearly winning" line, the low-clock line, and the endgame line.
- The explanation sentences that come from the engine lines ("lets your opponent take over: after X they are clearly better"). These are not tips, but they read as the same voice.
- Tag descriptions in `src/ChessCoach.Core/Training/TagCatalog.cs` and the weekly-focus advice that uses them.

Do this in one pass so the voice is consistent. Keep the meaning of each tip, and check how each reads inside the puzzle and focus cards.
