# ChessCoach

A Windows desktop chess coach built on your own chess.com games. It shows your stats, openings and opponents, finds the blunders you keep repeating, and trains you on them with puzzles and a daily drill.

It is a Blazor Hybrid app (WPF host, .NET 10, MudBlazor UI) with SQLite for storage. The analysis runs in Python, using Stockfish and Maia through lc0. See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the design.

## Install (for players)

ChessCoach runs on Windows 10 or 11. Setting it up takes a while the first time, because your games have to be downloaded and analysed. Analysing a few thousand games can take hours on a laptop.

### 1. Get the app

Download the build your coach sends you, or build it yourself (see [Development](#development)). Unzip it to a folder you can keep, such as `C:\Users\you\ChessCoach`. Run `ChessCoach.exe`.

If you want it on the taskbar, right-click `ChessCoach.exe`, choose **Show more options**, then **Pin to taskbar**.

### 2. Get the analysis tools

The analysis runs in Python, so you also need the repository (or at least its `analysis` folder):

- **Python 3.12 or newer**, from [python.org](https://www.python.org/downloads/). Tick "Add python.exe to PATH" when you install it.
- **Git**, to clone this repository, or download it as a zip from GitHub.

Then, in a PowerShell window in the repository folder:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\setup.ps1
```

This creates the Python environment in `analysis\.venv` and checks what is still missing.

### 3. Get the chess engines

Put these in your data folder (see step 4) under `engine\`. The file names matter, because the scripts look for these exact paths:

| Engine | Where to get it | Save as |
|---|---|---|
| Stockfish 19 or newer (Windows x86-64) | [stockfishchess.org](https://stockfishchess.org/download/) | `engine\stockfish\stockfish-windows-x86-64-universal.exe` |
| lc0 (Windows, CPU build is fine) | [lczero.org](https://lczero.org/dont_panic/lc0/) | `engine\lc0\lc0.exe` |
| Maia 1100 weights | [CSSLab/maia-chess](https://github.com/CSSLab/maia-chess) | `engine\maia\maia-1100.pb.gz` |

### 4. Choose a data folder

Your games, analysis and training progress live in one folder, which must not be inside the app folder. Create it, for example `C:\Users\you\chess-coach-data`, and put the engines in it.

Tell the app where it is with a file called `appsettings.local.json` **next to `ChessCoach.exe`**. It is read on start-up and is never shared:

```json
{
  "ChessCoach": {
    "DataDirectory": "C:/Users/you/chess-coach-data",
    "AnalysisDirectory": "C:/Users/you/ChessCoach/analysis"
  }
}
```

Use forward slashes (`/`). The file must be valid JSON, or the app will not start.

### 5. Import your games

Start the app once, so it creates the database in your data folder. Close it again.

Then, in a PowerShell window in the `analysis` folder, download your rapid games and analyse them. Replace `your_chess_com_name` with your chess.com username:

```powershell
.venv\Scripts\python.exe -I refresh.py --data-dir C:\Users\you\chess-coach-data --user your_chess_com_name
```

This is the slow step. It runs in stages, and you can stop it at any time and run it again: finished work is kept. Once it finishes, open the app and your Home page fills in.

To add newer games later, run the same command again. It only downloads and analyses what is new.

### Troubleshooting

- **The app closes straight away, or shows "could not start".** Check `appsettings.local.json` is valid JSON and both folders exist.
- **Home shows no data.** Step 5 has not finished, or `--data-dir` does not match `DataDirectory`.
- **Puzzles or drills are empty.** Their notes come from the analysis; run step 5 again and look for the stage that failed.
- **The page says WebView2 is missing.** Install the [WebView2 runtime](https://developer.microsoft.com/microsoft-edge/webview2/).
- **Anything else.** The log is in `<DataDirectory>\logs`. Send that file with your question.

## Development

Prerequisites: Visual Studio 2026 (or the .NET 10 SDK), Python 3.12+, the WebView2 runtime, and a data folder as above. For development, set `ChessCoach:DataDirectory` in `src\ChessCoach.Desktop\appsettings.json`, or use `appsettings.local.json` in the output folder so your paths stay out of git.

```powershell
powershell -ExecutionPolicy Bypass -File scripts\setup.ps1   # creates analysis\.venv and checks prerequisites
dotnet test ChessCoach.sln
dotnet run --project src\ChessCoach.Desktop
```

Build a self-contained copy for your own taskbar, or for a friend:

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
