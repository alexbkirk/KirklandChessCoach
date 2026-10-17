# ChessCoach

A Windows desktop chess coach (Blazor Hybrid on WPF, .NET 10) built on a player's own chess.com games. It replaces the static `dashboard.html` from the original prototype and reuses its SQLite database, engines (Stockfish, Maia via lc0) and Python analysis scripts.

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the design, build order and decisions.

## Getting started

Prerequisites: Visual Studio 2026 (or the .NET 10 SDK), Python 3.12+, the WebView2 runtime, and a data folder containing `xkirke_games.db` and `engine\` (set `ChessCoach:DataDirectory` in `src\ChessCoach.Desktop\appsettings.json`).

```powershell
powershell -ExecutionPolicy Bypass -File scripts\setup.ps1   # creates analysis\.venv and checks prerequisites
dotnet test ChessCoach.sln
dotnet run --project src\ChessCoach.Desktop
```

The app migrates the database on startup (`src\ChessCoach.Infrastructure\Data\Migrations`) and switches it to WAL mode.

## Layout

| Path | Purpose |
|---|---|
| `src/ChessCoach.Desktop` | WPF host, DI, configuration, logging |
| `src/ChessCoach.UI` | Razor components (MudBlazor) |
| `src/ChessCoach.Core` | Domain types and pure logic, no I/O |
| `src/ChessCoach.Infrastructure` | SQLite (Dapper), migrations, diagnostics |
| `analysis/` | Python scripts for fetching, analysing and tagging games |
| `tests/` | xUnit tests |

## Licences

- The app code is MIT-free for now; no licence has been chosen yet.
- `src/ChessCoach.UI/wwwroot/lib/chessground` is **GPL-3.0-or-later** (chessground). Distributing the app means it must be GPL-3.0-or-later too.
- `src/ChessCoach.UI/wwwroot/lib/chessjs` is BSD-2-Clause (chess.js).
