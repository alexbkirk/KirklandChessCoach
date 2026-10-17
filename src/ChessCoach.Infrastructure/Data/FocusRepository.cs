using System.Globalization;
using System.Text.Json;
using ChessCoach.Core.Abstractions;
using ChessCoach.Core.Domain;
using Dapper;

namespace ChessCoach.Infrastructure.Data;

public sealed class FocusRepository(SqliteConnectionFactory connections) : IFocusRepository
{
    private const string DateFormat = "yyyy-MM-dd";

    public async Task<(IReadOnlyList<FocusGame> Games, IReadOnlyList<FocusBlunder> Blunders)> LoadAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        var gameRows = await connection.QueryAsync<GameRow>(new CommandDefinition(
            "SELECT id AS Id, CAST(date AS TEXT) AS Date, CAST(opponent AS TEXT) AS Opponent, CAST(url AS TEXT) AS Url FROM games ORDER BY CAST(date AS TEXT), time",
            cancellationToken: cancellationToken));
        var blunderRows = await connection.QueryAsync<BlunderRow>(new CommandDefinition(
            """
            SELECT t.game_id AS GameId, t.ply AS Ply, t.tags AS Tags
            FROM blunder_tags t
            JOIN moves m ON m.game_id = t.game_id AND m.ply = t.ply
            WHERE m.is_mine = 1 AND m.is_last = 0 AND m.class = 'blunder'
              AND ((m.color = 'white' AND m.eval_before >= -100) OR (m.color = 'black' AND m.eval_before <= 100))
            """,
            cancellationToken: cancellationToken));

        var games = gameRows
            .Select(r => new FocusGame(r.Id, DateOnly.ParseExact(r.Date, DateFormat, CultureInfo.InvariantCulture), r.Opponent ?? "", r.Url ?? ""))
            .ToList();
        var blunders = blunderRows
            .Select(r => new FocusBlunder(r.GameId, r.Ply, TagKeys(r.Tags)))
            .ToList();
        return (games, blunders);
    }

    /// <summary>The "k" field of each tag object, e.g. [{"k":"missed_mate","l":"Missed mate"}].</summary>
    private static List<string> TagKeys(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateArray()
            .Select(e => e.GetProperty("k").GetString() ?? "")
            .Where(k => k.Length > 0)
            .ToList();
    }

    private sealed class GameRow
    {
        public string Id { get; set; } = "";

        public string Date { get; set; } = "";

        public string? Opponent { get; set; }

        public string? Url { get; set; }
    }

    private sealed class BlunderRow
    {
        public string GameId { get; set; } = "";

        public int Ply { get; set; }

        public string Tags { get; set; } = "[]";
    }
}
