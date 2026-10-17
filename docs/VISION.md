# Vision: an AI coach that knows your whole chess history

## Goal

Make me a better player. Use the familiar ways to improve (puzzles, game review, lessons, playing practice games), but with what online tools lack: the bigger picture from every rapid game I have played. In the end, play against an interactive coach that learns from those games and uses what it learns to keep improving my play. As close to a human coach as we can get.

## What a human coach does that today's tools don't

1. **Remembers you.** Your past games, what you struggled with last month, whether the work is paying off.
2. **Asks before telling.** "What were you worried about here?" Your reasons are the missing data; the engine only sees the move.
3. **Explains this position in words**, rather than picking a rule from a list.
4. **Plans the training.** One goal at a time, measured on your new games.

Example from the data: you reached +5 or better in 942 games and lost 301 of them. Most of those collapsed in the middlegame with about 7 minutes still on the clock, and 150 ended in checkmate. No puzzle site would tell you that your biggest leak is your king's safety when you are winning.

## Architecture

```
           ┌──────────────── Player model (the coach's memory) ────────────────┐
           │ stats from every game: phases, motifs, time use, openings,        │
           │ conversion, king safety · your answers to the coach's questions · │
           │ coach journal: goals set, what was tried, did it work             │
           └────────────▲───────────────────────────────┬──────────────────────┘
                        │ updated after each game       │ read for every interaction
 chess.com games ──► analysis (Stockfish = truth, Maia = how humans at your level play, motifs)
                        │                               ▼
                        │                    Coach (Claude, with tools)
                        │                    - calls the engine, checks legal moves,
                        │                      looks up your stats and past games
                        │                    - never invents an evaluation; it explains
                        │                      what the tools return
                        │                               │
                        └──── new games, answers ◄──────┤
                                                        ▼
     Game review · Play vs coach · Lessons from your own positions · Puzzles/drills · Weekly plan
```

The engine and motif code (`analysis/motifs.py`, the coach notes) become the coach's tools rather than its final words.

## Decisions so far

| Question | Decision |
|---|---|
| Use an LLM? | Yes: Anthropic's API, with your own key. The key is saved only on this computer, encrypted for your Windows account in `%LOCALAPPDATA%\ChessCoach`. It never goes in the database, the settings file or the repository. Set it on the Settings page. |
| Coaching style | **Socratic.** The coach asks what you see and what you were thinking before it explains. It gives hints in steps and the answer last. |
| Practice opponent | **Slightly above your level.** Maia models exist in 100-point steps from 1100 to 1900. Only 1100 is installed today, so stronger levels need their weights downloading. |
| First mode to build | Open (see below). |

## Ways to work with the coach

| Mode | What it does | What the coach learns |
|---|---|---|
| **Game review** | After each new chess.com game, it goes through 3–5 critical moments: first asking what you were thinking, then explaining. | Why you play your mistakes: missed threats, wrong plans, time panic |
| **Play vs coach** | A game in the app against a Maia bot slightly above your level. The coach can step in at critical moments or stay quiet until the end. | How you play in real time, and which hints help |
| **Lessons from your own positions** | Example: a "convert the win" course where you play out your own +5 positions against the engine until you hold them. | Whether the leak is closing |
| **Puzzles and drills** | What exists today, but chosen by the plan and explained by the coach | Recognition speed |
| **Weekly plan** | Sets one goal, measures it on new games, adjusts | Whether the training works |

## Roadmap

1. **Foundations (now).** API key storage (done). Coach notes with named tactics (PR #6).
2. **Coach service.** A C# client for the Claude API, with tools: engine evaluation, legal moves, motif lookup, player stats, a game's moves. A player-model snapshot it reads first. A conversation log saved in the database.
3. **First mode**, Socratic throughout.
4. **Player model and journal.** Your answers and the coach's observations are stored and fed back in; weekly goals are measured on new games.
5. **The other modes.**
