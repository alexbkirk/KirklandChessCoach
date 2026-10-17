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
