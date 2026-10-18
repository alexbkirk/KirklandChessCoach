# ChessCoach

A Windows desktop chess coach built on your own chess.com games. It shows your stats, openings and opponents, finds the blunders you keep repeating, and trains you on them with puzzles and a daily drill.

It is a Blazor Hybrid app (WPF host, .NET 10, MudBlazor UI) with SQLite for storage. The analysis runs in Python, using Stockfish and Maia through lc0. See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the design.

## Install (for players)

1. Download **ChessCoach-Setup.exe** from the link your coach sent you.
2. Double-click it and follow the steps. Nothing else needs installing: Python and the chess engines are included or downloaded for you.
3. ChessCoach opens on a short setup page. Work through the three steps:
   - **Your chess.com username**, the name you play under.
   - **Download the chess engines** (about 100 MB, once).
   - **Import your games**. This can take several hours. You can pause it, close ChessCoach, and carry on later.

Windows may say the app is from an unknown publisher, because the installer is not code-signed yet. Choose **More info**, then **Run anyway**.

Your games and settings are kept in `%LOCALAPPDATA%\ChessCoach`. Uninstalling the app does not delete them.

### If something goes wrong

- **The setup page says the download did not finish.** Check your internet connection and press the button again.
- **The import stopped with an error.** The message shows what happened. Press **Start the import** again: it carries on from where it stopped.
- **Anything else.** The log is in `%LOCALAPPDATA%\ChessCoach\logs`. Send that file with your question.

## Development

Prerequisites: Visual Studio 2026 (or the .NET 10 SDK), Python 3.12+, the WebView2 runtime, and a data folder as above. For development, set `ChessCoach:DataDirectory` in `src\ChessCoach.Desktop\appsettings.json`, or use `appsettings.local.json` in the output folder so your paths stay out of git.

```powershell
powershell -ExecutionPolicy Bypass -File scripts\setup.ps1   # creates analysis\.venv and checks prerequisites
dotnet test ChessCoach.sln
dotnet run --project src\ChessCoach.Desktop
```

Build the installer (it downloads Python, bundles the analysis and compiles `dist\ChessCoach-Setup.exe`; needs Inno Setup 6, `winget install JRSoftware.InnoSetup`):

```powershell
powershell -ExecutionPolicy Bypass -File tools\build-installer.ps1
```

Or a self-contained copy without the installer:

```powershell
dotnet publish src\ChessCoach.Desktop -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o C:\Users\you\ChessCoach-app
```

The app migrates the database on startup (`src\ChessCoach.Infrastructure\Data\Migrations`) and switches it to WAL mode.

Python tests run with:

```powershell
analysis\.venv\Scripts\python -m unittest discover -s analysis\tests
```

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
