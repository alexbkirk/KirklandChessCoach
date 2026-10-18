using System.Globalization;
using ChessCoach.Core.Abstractions;
using ChessCoach.Core.Domain;
using Dapper;

namespace ChessCoach.Infrastructure.Data;

public sealed class StatsRepository(SqliteConnectionFactory connections) : IStatsRepository
{
    public async Task<IReadOnlyList<StatsGame>> LoadGamesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        // Untyped columns in the Python-created table: cast text, and map through settable properties (see ARCHITECTURE.md).
        var rows = await connection.QueryAsync<Row>(new CommandDefinition(
            """
            SELECT id AS Id, CAST(date AS TEXT) AS Date, CAST(time AS TEXT) AS Time, CAST(color AS TEXT) AS Color,
                   CAST(result AS TEXT) AS Result, myRating AS MyRating, oppRating AS OppRating,
                   CAST(timeControl AS TEXT) AS TimeControl, CAST(family AS TEXT) AS Family,
                   CAST(ending AS TEXT) AS Ending, plies AS Plies, CAST(opponent AS TEXT) AS Opponent
            FROM games
            ORDER BY CAST(date AS TEXT), CAST(time AS TEXT)
            """,
            cancellationToken: cancellationToken));

        return rows.Select(r => new StatsGame(
            Id: r.Id,
            Date: DateOnly.ParseExact(r.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture),
            Time: r.Time ?? "",
            Color: r.Color ?? "",
            Result: r.Result ?? "",
            MyRating: r.MyRating,
            OppRating: r.OppRating,
            TimeControl: r.TimeControl ?? "",
            Family: r.Family ?? "",
            Ending: r.Ending ?? "",
            Plies: r.Plies,
            Opponent: r.Opponent ?? "")).ToList();
    }

    public async Task<IReadOnlyList<MoveSample>> LoadMoveSamplesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<MoveRow>(new CommandDefinition(
            """
            SELECT CAST(game_id AS TEXT) AS GameId, ply AS Ply, CAST(color AS TEXT) AS Color,
                   eval_before AS EvalBefore, cp_loss AS CpLoss, CAST(class AS TEXT) AS Class, clk AS Clock
            FROM moves
            WHERE is_mine = 1 AND is_last = 0
            """,
            cancellationToken: cancellationToken));

        return rows.Select(r => new MoveSample(
            GameId: r.GameId,
            Ply: r.Ply,
            Color: r.Color ?? "",
            EvalBefore: r.EvalBefore,
            CpLoss: r.CpLoss,
            IsBlunder: r.Class == "blunder",
            Clock: r.Clock)).ToList();
    }

    private sealed class MoveRow
    {
        public string GameId { get; set; } = "";

        public int Ply { get; set; }

        public string? Color { get; set; }

        public int EvalBefore { get; set; }

        public int CpLoss { get; set; }

        public string? Class { get; set; }

        public double? Clock { get; set; }
    }

    private sealed class Row
    {
        public string Id { get; set; } = "";

        public string Date { get; set; } = "";

        public string? Time { get; set; }

        public string? Color { get; set; }

        public string? Result { get; set; }

        public int MyRating { get; set; }

        public int OppRating { get; set; }

        public string? TimeControl { get; set; }

        public string? Family { get; set; }

        public string? Ending { get; set; }

        public int Plies { get; set; }

        public string? Opponent { get; set; }
    }
}
