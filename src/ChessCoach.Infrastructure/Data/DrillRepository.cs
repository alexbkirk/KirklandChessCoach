using System.Globalization;
using System.Text.Json;
using ChessCoach.Core.Abstractions;
using ChessCoach.Core.Domain;
using ChessCoach.Core.Settings;
using Dapper;

namespace ChessCoach.Infrastructure.Data;

public sealed class DrillRepository(SqliteConnectionFactory connections, TimeClassView? timeClass = null) : IDrillRepository
{
    private const string DayFormat = "yyyy-MM-dd";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // One-move blunders: positions where the move lost material within the opponent's next two moves.
    private const string BlunderCandidates = """
        FROM blunder_tags t
        JOIN moves m ON m.game_id = t.game_id AND m.ply = t.ply
        JOIN games g ON g.id = m.game_id
        WHERE t.tags LIKE '%"one_move"%' AND (@tc IS NULL OR g.timeClass = @tc)
        """;

    // Sound moves from roughly balanced middlegame positions, as the "safe" side of safe-or-blunder.
    private const string SafeCandidates = """
        FROM moves m
        JOIN games g ON g.id = m.game_id
        WHERE m.is_mine = 1 AND m.is_last = 0 AND m.class IN ('best', 'good')
          AND m.ply BETWEEN 10 AND 70 AND ABS(m.eval_before) <= 300
          AND (@tc IS NULL OR g.timeClass = @tc)
        """;

    private const string PositionColumns = """
        SELECT m.game_id AS GameId, m.ply AS Ply, m.fen_before AS Fen, m.color AS Color, m.uci AS Uci, m.san AS San,
               m.best_san AS BestSan, m.cp_loss AS CpLoss, m.eval_before AS EvalBefore, m.eval_after AS EvalAfter,
               m.class AS Class, CAST(g.date AS TEXT) AS Date, g.opponent AS Opponent, g.url AS Url,
               (SELECT n.note FROM blunder_notes n WHERE n.game_id = m.game_id AND n.ply = m.ply) AS Note
        """;

    public async Task<(int Blunders, int Safe)> CountCandidatesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        var blunders = await connection.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) " + BlunderCandidates, new { tc = timeClass?.Current }, cancellationToken: cancellationToken));
        var safe = await connection.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) " + SafeCandidates, new { tc = timeClass?.Current }, cancellationToken: cancellationToken));
        return (blunders, safe);
    }

    public async Task<DrillPosition?> GetCandidateAsync(bool blunder, int index, CancellationToken cancellationToken = default)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        var row = await connection.QueryFirstOrDefaultAsync<PositionRow>(new CommandDefinition(
            PositionColumns + " " + (blunder ? BlunderCandidates : SafeCandidates) + " ORDER BY m.game_id, m.ply LIMIT 1 OFFSET @index",
            new { index, tc = timeClass?.Current },
            cancellationToken: cancellationToken));
        return row?.ToPosition();
    }

    public async Task<IReadOnlyList<DrillSlot>?> LoadDayAsync(DateOnly day, CancellationToken cancellationToken = default)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        var rows = (await connection.QueryAsync<SlotRow>(new CommandDefinition(
            "SELECT idx AS Idx, position_id AS PositionId, format AS Format, state_json AS StateJson, done AS Done, score AS Score FROM drill_slots WHERE day = @day ORDER BY idx",
            new { day = Format(day) },
            cancellationToken: cancellationToken))).ToList();
        if (rows.Count == 0)
        {
            return null;
        }

        var slots = new List<DrillSlot>();
        foreach (var row in rows)
        {
            var separator = row.PositionId.LastIndexOf('_');
            var position = await connection.QueryFirstOrDefaultAsync<PositionRow>(new CommandDefinition(
                PositionColumns + " FROM moves m JOIN games g ON g.id = m.game_id WHERE m.game_id = @gameId AND m.ply = @ply",
                new { gameId = row.PositionId[..separator], ply = int.Parse(row.PositionId[(separator + 1)..], CultureInfo.InvariantCulture) },
                cancellationToken: cancellationToken));
            if (position is null)
            {
                continue;   // the game was removed from the database; skip its slot
            }

            var state = JsonSerializer.Deserialize<SlotState>(row.StateJson, JsonOptions) ?? new SlotState();
            slots.Add(new DrillSlot
            {
                Index = row.Idx,
                Format = Enum.Parse<DrillFormat>(row.Format, ignoreCase: true),
                Position = position.ToPosition(),
                Round = state.Round,
                Answers = state.Answers ?? [],
                Choice = state.Choice,
                Done = row.Done != 0,
                Score = row.Score,
            });
        }

        return slots;
    }

    public async Task SaveNewDayAsync(DateOnly day, int seed, IReadOnlyList<DrillSlot> slots, CancellationToken cancellationToken = default)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await connection.ExecuteAsync("INSERT OR REPLACE INTO drill_days(day, seed) VALUES (@day, @seed)", new { day = Format(day), seed }, transaction);
        foreach (var slot in slots)
        {
            await UpsertSlotAsync(connection, transaction, day, slot);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task SaveSlotAsync(DateOnly day, DrillSlot slot, CancellationToken cancellationToken = default)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        await UpsertSlotAsync(connection, null, day, slot);
    }

    public async Task<IReadOnlySet<DateOnly>> LoadCompletedDaysAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        var days = await connection.QueryAsync<string>(new CommandDefinition(
            "SELECT day FROM drill_slots GROUP BY day HAVING MIN(done) = 1",
            cancellationToken: cancellationToken));
        return days.Select(d => DateOnly.ParseExact(d, DayFormat, CultureInfo.InvariantCulture)).ToHashSet();
    }

    private static Task<int> UpsertSlotAsync(System.Data.Common.DbConnection connection, System.Data.Common.DbTransaction? transaction, DateOnly day, DrillSlot slot) =>
        connection.ExecuteAsync(
            """
            INSERT INTO drill_slots(day, idx, position_id, format, state_json, done, score)
            VALUES (@Day, @Idx, @PositionId, @Format, @StateJson, @Done, @Score)
            ON CONFLICT(day, idx) DO UPDATE SET
              position_id = excluded.position_id, format = excluded.format, state_json = excluded.state_json,
              done = excluded.done, score = excluded.score
            """,
            new
            {
                Day = Format(day),
                Idx = slot.Index,
                slot.Position.PositionId,
                Format = slot.Format.ToString().ToLowerInvariant(),
                StateJson = JsonSerializer.Serialize(new SlotState { Round = slot.Round, Answers = slot.Answers, Choice = slot.Choice }, JsonOptions),
                Done = slot.Done ? 1 : 0,
                slot.Score,
            },
            transaction);

    private static string Format(DateOnly day) => day.ToString(DayFormat, CultureInfo.InvariantCulture);

    private sealed class SlotState
    {
        public int Round { get; set; }

        public List<DrillAnswer>? Answers { get; set; }

        public string? Choice { get; set; }
    }

    private sealed class SlotRow
    {
        public int Idx { get; set; }

        public string PositionId { get; set; } = "";

        public string Format { get; set; } = "";

        public string StateJson { get; set; } = "{}";

        public int Done { get; set; }

        public int Score { get; set; }
    }

    private sealed class PositionRow
    {
        public string GameId { get; set; } = "";

        public int Ply { get; set; }

        public string Fen { get; set; } = "";

        public string Color { get; set; } = "";

        public string Uci { get; set; } = "";

        public string San { get; set; } = "";

        public string? BestSan { get; set; }

        public int CpLoss { get; set; }

        public int EvalBefore { get; set; }

        public int EvalAfter { get; set; }

        public string Class { get; set; } = "";

        public string Date { get; set; } = "";

        public string Opponent { get; set; } = "";

        public string Url { get; set; } = "";

        public string? Note { get; set; }

        public DrillPosition ToPosition() => new(
            $"{GameId}_{Ply}", Fen, Color, Uci, San, BestSan ?? "", CpLoss, EvalBefore, EvalAfter,
            IsBlunder: Class == "blunder", Date, Opponent, Url,
            Note: Note is null ? null : NoteJson.Parse(Note)?.ToCoachNote());
    }
}
