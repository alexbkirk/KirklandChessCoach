# ChessCoach architecture

A Windows desktop chess coach built on the data in `C:\Users\alexb\chess-coach-data`. It replaces the static `dashboard.html`. The existing SQLite database, the engines and the Python analysis scripts are kept.

Status: **step 1 (skeleton) is built**; steps 2 to 5 below are not. This document records the decisions made before scaffolding, plus a short "Step 1 as built" section at the end for where the code differs from the plan.

## Decisions

| Area | Choice | Why |
|---|---|---|
| Runtime | .NET 10 (LTS, supported to Nov 2028) | .NET 8 support ends Nov 10, 2026 |
| IDE | Visual Studio 2026 Community | VS 2022 17.10 cannot target .NET 10 |
| Host | WPF + `BlazorWebView` (Blazor Hybrid) | Simplest Windows-only Hybrid host; a real window, no server to start |
| Projects | 4 projects + 2 test projects | Thin host, pure testable core, swappable UI host |
| Data access | Dapper + numbered SQL migrations | Explicit SQL for a database that Python also writes to |
| UI kit | MudBlazor | Tabs, data grids, dialogs, snackbars and progress bars out of the box |
| Board | cm-chessboard (MIT) + chess.js (BSD-2), vendored | Permissive licences; chessground (GPL-3) is the alternative |
| Charts | MudBlazor charts first, Blazor-ApexCharts only if needed | Avoid a second UI dependency until a chart needs it |
| Analysis | Existing Python scripts, called as a child process | Reuse working code; port to C# later only if wanted |
| Data folder | Stays at `C:\Users\alexb\chess-coach-data`, set in `appsettings.json` | Code and data stay separate; the database is not in git |

## Runtime overview

```
┌─────────────── ChessCoach.Desktop (WPF window) ───────────────┐
│  BlazorWebView ── ChessCoach.UI (Razor components, MudBlazor)  │
│        │                 │ JS interop: board (cm-chessboard)  │
│        ▼                 ▼                                     │
│  ChessCoach.Core (domain, stats, training rules, interfaces)   │
│        ▲                                                       │
│  ChessCoach.Infrastructure                                     │
│    ├─ Data: SQLite via Dapper ───────────► xkirke_games.db     │
│    ├─ Engines: UCI client ───────────────► stockfish.exe, lc0.exe + Maia 1100
│    └─ Pipeline: Python runner ───────────► analysis\refresh.py │
└────────────────────────────────────────────────────────────────┘
          refresh.py ── fetches chess.com, runs Stockfish/Maia, writes rows ──► xkirke_games.db
```

## Solution layout

```
ChessCoach\
├─ ChessCoach.sln                (classic .sln format, not .slnx)
├─ global.json                   pins the .NET 10 SDK
├─ Directory.Build.props         Nullable, ImplicitUsings, TreatWarningsAsErrors, AnalysisLevel
├─ Directory.Packages.props      central NuGet versions
├─ .editorconfig  .gitignore  README.md
├─ docs\ARCHITECTURE.md
├─ src\
│  ├─ ChessCoach.Desktop\        net10.0-windows, WPF: App, MainWindow, Generic Host, DI, appsettings.json, logging
│  ├─ ChessCoach.UI\             Razor class library
│  │  ├─ Pages\                  Home, Overview, Openings, Blunders, Habits, Games, Settings
│  │  ├─ Components\             Board, Puzzles, Drill, Focus, OpeningReport, Charts, Common
│  │  ├─ State\                  FilterState and other UI state services
│  │  └─ wwwroot\                css, js\board-interop.js, lib\ (cm-chessboard, chess.js)
│  ├─ ChessCoach.Core\           net10.0, no I/O
│  │  ├─ Domain\                 Game, MoveEval, Blunder, BlunderTag, CoachNote, DrillPosition
│  │  ├─ Stats\                  results, opening report + verdicts, weekly focus
│  │  ├─ Training\               spaced repetition, puzzle grading, drill day builder
│  │  └─ Abstractions\           IGameRepository, IProgressRepository, IEngine, IAnalysisPipeline, IClock
│  └─ ChessCoach.Infrastructure\
│     ├─ Data\                   SqliteConnectionFactory, repositories, MigrationRunner, Migrations\*.sql
│     ├─ Engines\                UciEngine, StockfishEngine, MaiaEngine
│     └─ Pipeline\               PythonPipelineRunner (JSON-lines progress, cancellation)
├─ analysis\                     refresh.py, analyze.py, tag_blunders.py, coach.py, parse_games.py, tagging.py
│  └─ requirements.txt           chess==1.11.2   (.venv\ is gitignored)
├─ scripts\
│  ├─ setup.ps1                  creates analysis\.venv, checks engines and WebView2
│  └─ publish.ps1                self-contained win-x64 publish
└─ tests\
   └─ ChessCoach.Infrastructure.Tests\       (ChessCoach.Core.Tests is added in step 2, with the first Core logic)
```

### Dependency rules

- `Core` references nothing in the solution.
- `Infrastructure` and `UI` reference `Core` only. The UI never touches SQLite, processes or files directly; it goes through `Core.Abstractions`.
- `Desktop` references all three and is the only place implementations are wired into DI.
- `UI` stays host-agnostic, so it could later run in MAUI or a Blazor web host.

## Data

### Location and connections

- The database path comes from `appsettings.json` (`ChessCoach:DataDirectory`).
- At startup the app runs migrations and sets `PRAGMA journal_mode=WAL` (persistent), so the UI can read while a Python refresh writes. The database is currently in `delete` journal mode.
- Every connection, in C# and Python, uses a busy timeout of 5 s.

### Schema ownership

- **The app's migrations own the schema.** `Infrastructure\Data\Migrations\NNNN_name.sql` are embedded resources. A hand-rolled `MigrationRunner` (about 50 lines, no DbUp) applies them in order, each inside a transaction, and records them in `schema_version`.
- `0001_baseline.sql` reproduces today's schema exactly with `CREATE TABLE IF NOT EXISTS`, so it is a no-op on the existing database and creates a correct empty database for tests.
- The Python scripts stop issuing `CREATE`/`ALTER`. They only read and write rows. Because refresh is always launched by the app, migrations have run before Python touches the file.

### Existing tables (rows written by Python)

```sql
CREATE TABLE games (id, url, date, time, color, result, ending, timeControl, timeClass, myRating, oppRating,
                    opponent, eco, opening, family, plies, moves, pgn, primary key(id));   -- untyped columns, kept as-is
CREATE TABLE moves(game_id text, ply int, color text, is_mine int, san text, uci text, fen_before text,
                   best_uci text, best_san text, eval_before int, eval_after int, cp_loss int, class text,
                   is_last int default 0, clk real, primary key(game_id, ply));
CREATE TABLE game_analysis(game_id text primary key, depth int, my_acpl real, n_my_moves int,
                           inaccuracies int, mistakes int, blunders int);
CREATE TABLE blunder_tags(game_id text, ply int, tags text, primary key(game_id, ply));   -- tags = JSON array
CREATE TABLE coach_notes(game_id text, ply int, note text, primary key(game_id, ply));    -- note = JSON object
```

Conventions inherited from the pipeline:

- Evaluations are centipawns from White's point of view.
- Mate scores are ±10000 minus the mate distance.
- `cp_loss` is capped at 1000.
- `is_last` marks the final move of a game, which is excluded from blunder statistics.
- `clk` is seconds left after the move.

### New tables (owned by the app)

```sql
-- 0002_training_progress.sql
CREATE TABLE puzzle_progress(position_id text primary key,   -- '<game_id>_<ply>'
  box int not null, due_utc text not null, tries int not null, correct int not null, last_seen_utc text);
CREATE TABLE puzzle_attempts(id integer primary key, position_id text not null, played_uci text,
  correct int not null, gave_up int not null, hint_used int not null, at_utc text not null);
CREATE TABLE drill_days(day text primary key, seed int not null);          -- local date 'YYYY-MM-DD'
CREATE TABLE drill_slots(day text, idx int, position_id text not null, format text not null,  -- hang | cct | safe
  state_json text not null, done int not null, score int not null, primary key(day, idx));
CREATE TABLE app_settings(key text primary key, value text);
```

`puzzle_attempts` keeps the full history, which feature 4 (progress trend) needs.

## Analysis pipeline (Python)

- The scripts move from `chess-coach-data` to `analysis\`, with a virtual environment created by `scripts\setup.ps1` from `requirements.txt`.
- `build_dashboard.py` and the `xkirke_games.json` export are retired once the app reaches parity.
- Invocation contract:

  ```
  analysis\.venv\Scripts\python.exe -I analysis\refresh.py --data-dir <dir> --user xkirke --progress jsonl
  ```

  - stdout: one JSON object per line, for example
    `{"stage":"analyze","done":12,"total":40,"message":"Analysing game 12 of 40"}`.
    The stages are `fetch`, `analyze`, `tag`, `coach` and `done`.
  - stderr: free-text log. The exit code is 0 on success.
  - Cancellation: the runner kills the process tree. Every stage is already resumable, because it skips work that is already stored.
- Changes needed in the scripts:
  - add `--data-dir` and `--progress`;
  - replace the hard-coded `H`-relative paths;
  - remove `CREATE`/`ALTER` statements;
  - set `busy_timeout`.
- The `/refresh-chess` skill in `chess-coach-data\.claude\skills` gets pointed at the new location. Eventually the app's Refresh button replaces it.

## Live engines (C#)

- `UciEngine` starts one long-lived process per engine and talks UCI over stdin and stdout with an async line reader. It runs one search at a time per engine through a queue, with timeouts and cancellation.
- `StockfishEngine` reads its threads and hash size from settings.
- `MaiaEngine` is lc0 with `maia-1100.pb.gz`, using its policy output at 1 node with `VerboseMoveStats`, the same parsing as `coach.py`.
- The binaries stay in `<DataDirectory>\engine\`, with paths in settings.
- First uses: play a puzzle's continuation after you answer, and coach any position on demand.

## UI

- MudBlazor layout: an app bar, then navigation to Home, Overview, Openings, Blunders, Habits, Games and Settings.
- Settings shows diagnostics: data folder, database size, the Python and engine paths with a check for each, and the last refresh.
- `ChessBoard` component: wraps `board-interop.js`, an ES module loaded with `IJSObjectReference`.
  - API: `SetPosition(fen, orientation)`, `EnableMoveInput()` with a C# callback receiving a UCI move, plus markers and arrows.
  - Legal moves and SAN come from chess.js on the JavaScript side. The C# side deals in FEN and UCI strings only.
- Updates triggered by background work, such as pipeline progress or engine output, reach components through `InvokeAsync(StateHasChanged)`.

## Behaviour to port (current rules)

These rules are implemented in the current `dashboard_template.html` and the Python scripts, and should carry over unchanged.

- **Puzzles (spaced repetition):**
  - The pool is the 100 coached blunders.
  - A move is correct if it is in the stored `accept` list: Stockfish depth 16, MultiPV 5, within 50 cp of the best move.
  - Boxes 0–6 with review intervals of 0, 1, 3, 7, 14, 30 and 60 days. A miss sends the puzzle to box 0, due again in 5 minutes.
  - A set holds 5 puzzles: due ones first (lowest box, then oldest due), then unseen ones (costliest first), then the next due.
- **Daily drill:**
  - 3 slots a day, chosen deterministically from the local date.
  - Each slot gets a random format: `hang`, `cct` (3 rounds: checks, captures, threats) or `safe`.
  - The pool is the one-move blunders plus about 400 safe moves (classed best or good, ply 10–70, |eval| ≤ 300).
  - The answers (hanging squares, check squares, capture squares) are computed with `tagging.hanging_squares` and the legal-move rules.
  - The streak counts consecutive days with all 3 slots done.
- **Weekly focus:**
  - The window is the games from the last 30 days before the latest game. If that is fewer than 15 games, it uses the last 50 games.
  - Only blunders from balanced positions count, and a blunder can carry several tags.
  - The focus is the most frequent tag other than `other`. Each tag is compared with your lifetime rate per game.
- **Opening report:**
  - Per color, the top 3 families, each with its top 8 variations.
  - "First slip" is the first of your first 15 moves that loses 100 cp or more.
  - "Eval @10" is the evaluation from your side after your 10th move.
  - Verdict: **Fix** when the opening has 20+ games and its win rate is ≤ your color average − 3, or its eval @10 is ≤ −0.5. **Keep** when it has 20+ games, its win rate is ≥ your average + 3 and its eval @10 is ≥ 0. Otherwise **Watch**.
- **Blunder definitions:**
  - A blunder loses 200+ cp in one move.
  - A position counts as balanced when your side's evaluation before the move is ≥ −100.
  - Time pressure means 60 s or less left after the move.
  - Rapid means chess.com's rapid class: base time plus 40 × increment ≥ 600 s.

## Testing

- `Core.Tests` (xUnit):
  - the scheduler, puzzle grading, drill-day determinism, and the focus, opening-verdict and stats rules;
  - expected values taken from the current dashboard as golden numbers.
- `Infrastructure.Tests`:
  - migrations on an empty database and on a copy of the real schema;
  - repository round-trips;
  - `PythonPipelineRunner` against a fake script;
  - `UciEngine` against the real Stockfish, in an `Integration` category.
- Component tests with bUnit, added once the UI settles.

## Conventions

- `net10.0` (and `net10.0-windows` for Desktop), file-scoped namespaces, records for domain types, and `IClock` instead of `DateTime.Now` in logic.
- Logging uses Serilog through `Microsoft.Extensions.Logging`, writing to `<DataDirectory>\logs`.
- Git workflow: `main` plus feature branches. `.gitignore` covers `bin/`, `obj/`, `.vs/`, `analysis/.venv/` and `*.db`.

## Build order

1. **Skeleton.** The solution, props files and git. Migrations 0001 and 0002 with WAL. The Python scripts moved, with `--data-dir`, `--progress` and the venv. The Settings page with diagnostics.
   *Done when* the app opens a window, shows diagnostics for the real data folder, and the tests pass.
2. **Home.**
   - Board component.
   - Puzzles with spaced repetition saved to `puzzle_progress` and `puzzle_attempts`.
   - The daily drill, weekly focus and opening report.
3. **Stats tabs.** Overview, Openings, Blunders, Habits and Games at parity with `dashboard.html`.
4. **Refresh and live engine.** A Refresh button with a progress bar and cancel. Puzzle continuations and coaching on demand.
5. **New features 4–7.** Progress trend, the mate-in-N trainer, last-game review and endgame drills.

## Prerequisites

- Visual Studio 2026 Community with the **.NET desktop development** and **ASP.NET and web development** workloads (installs the .NET 10 SDK).
- WebView2 runtime: present (155.0).
- Python 3.14 with the `chess` package: present. `setup.ps1` creates a dedicated venv.
- Stockfish 19, lc0 0.32.1 and the Maia 1100 weights: present in `chess-coach-data\engine\`.

## Later / open

- Port the analysis pipeline to C#, to drop the Python dependency.
- Optional one-off import of browser puzzle and drill progress, from `progress.json` or a localStorage export.
- Packaging: a self-contained publish plus a Start-menu shortcut.

## Step 1 as built

Differences from the plan above, and things learned while building.

- **Solution format:** `ChessCoach.sln` (classic), because `dotnet new sln` on .NET 10 defaults to `.slnx`; the classic format works everywhere.
- **Desktop target framework:** `net10.0-windows10.0.19041.0`, not plain `net10.0-windows`. The WebView2 control loads the Windows SDK projection assembly (`Microsoft.Windows.SDK.NET`) at runtime, which only exists when the TFM carries a Windows version. With the unversioned TFM the app crashes right after the window opens.
- **Migrations:** three, not two. `0003_indexes.sql` adds indexes for the dashboard queries. `0001` is a no-op on the real database and builds the same schema in an empty one (covered by a test that starts from the Python-era DDL and checks rows survive).
- **Journal mode:** the first app start switched the real database to WAL. A pre-migration copy was kept outside the repo.
- **Untyped columns:** the Python-created `games` table declares no column types, so SQLite reports computed or NULL results as BLOB and Dapper then fails to map strings. Rule: `CAST(... AS TEXT)` explicitly for text results of expressions, and map through classes with settable properties rather than positional records. A later migration could add typed views.
- **Analyzers:** `AnalysisLevel=latest-recommended` plus `TreatWarningsAsErrors` flags `ILogger` calls (CA1848): use `[LoggerMessage]` source generation. CA1707 (underscores in names) is switched off for tests in `tests\.editorconfig`.
- **Core.Tests:** not created yet. An empty xUnit project fails `dotnet test`, and Core only holds interfaces and records so far.
- **Python:** `analysis\common.py` is new. It provides `--data-dir` (or `CHESSCOACH_DATA_DIR`), `--progress jsonl` and a busy-timeout connection. The scripts no longer create or alter tables and `refresh.py` no longer builds the old dashboard or `xkirke_games.json`. Verified end to end on a copy of the data: a game played after the last refresh was fetched, analysed and tagged, and the JSON progress lines were emitted. `build_dashboard.py`, `dashboard_template.html` and `.claude\skills\refresh-chess` stay in `C:\Users\alexb\chess-coach-data` as the legacy dashboard until step 3 reaches parity; that copy of the scripts is now stale and should not be used to write to the database.
- **Run it:** `powershell -File scripts\setup.ps1` once, then open `ChessCoach.sln` in Visual Studio 2026 and run `ChessCoach.Desktop`, or `dotnet run --project src\ChessCoach.Desktop`. Logs go to `<DataDirectory>\logs`.
- **Not yet checked:** the Settings page was only compiled and covered by service-level tests; its rendering has not been looked at.
