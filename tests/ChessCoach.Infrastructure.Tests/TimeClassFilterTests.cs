using ChessCoach.Core.Abstractions;
using ChessCoach.Core.Settings;
using ChessCoach.Infrastructure.Data;

namespace ChessCoach.Infrastructure.Tests;

public sealed class TimeClassFilterTests
{
    private sealed class MemoryStore : IAppSettingsStore
    {
        private readonly Dictionary<string, string> _values = [];

        public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_values.TryGetValue(key, out var value) ? value : null);

        public Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
        {
            _values[key] = value;
            return Task.CompletedTask;
        }
    }

    /// <summary>A database with one rapid and one blitz game, each with two of the player's moves, one blunder, and the same opponent.</summary>
    private static async Task<TempDatabase> SetUpAsync()
    {
        var db = new TempDatabase();
        await db.Migrations.MigrateAsync();
        await db.ExecuteAsync(
            """
            INSERT INTO games(id, url, date, time, color, result, myRating, oppRating, timeControl, timeClass, family, opening, ending, plies, opponent) VALUES
              ('r1', 'https://x/r1', '2026-10-10', '10:00:00', 'white', 'win',  900, 950, '600', 'rapid', 'Italian Game', 'Italian Game', 'checkmate', 40, 'Dana'),
              ('b1', 'https://x/b1', '2026-10-11', '10:00:00', 'white', 'loss', 800, 850, '180', 'blitz', 'Italian Game', 'Italian Game', 'timeout',   30, 'Dana');
            INSERT INTO moves(game_id, ply, color, is_mine, is_last, san, uci, fen_before, eval_before, eval_after, cp_loss, class) VALUES
              ('r1', 1, 'white', 1, 0, 'e4', 'e2e4', 'F', 10, 12, 2, 'best'),
              ('r1', 3, 'white', 1, 0, 'Qh5', 'd1h5', 'F', 12, -300, 312, 'blunder'),
              ('b1', 1, 'white', 1, 0, 'd4', 'd2d4', 'F', 10, 12, 2, 'best'),
              ('b1', 3, 'white', 1, 0, 'Bb5', 'f1b5', 'F', 12, -250, 262, 'blunder');
            """);
        return db;
    }

    private static async Task<TimeClassView> ViewAsync(string selection)
    {
        var view = new TimeClassView(new TimeClassSettings(new MemoryStore()));
        await view.SetImportedAsync(["blitz", "bullet"]);
        await view.SelectAsync(selection);
        return view;
    }

    [Theory]
    [InlineData("rapid", new[] { "r1" })]
    [InlineData("blitz", new[] { "b1" })]
    [InlineData("bullet", new string[0])]
    [InlineData("all", new[] { "r1", "b1" })]
    public async Task Stats_load_only_the_games_and_moves_of_the_selected_time_class(string selection, string[] expectedGames)
    {
        using var db = await SetUpAsync();
        var repository = new StatsRepository(db.Connections, await ViewAsync(selection));

        var games = await repository.LoadGamesAsync();
        var moves = await repository.LoadMoveSamplesAsync();

        Assert.Equal(expectedGames, games.Select(g => g.Id));
        Assert.Equal(expectedGames.Length * 2, moves.Count);
        Assert.All(moves, m => Assert.Contains(m.GameId, expectedGames));
    }

    [Fact]
    public async Task Without_a_view_nothing_is_filtered()
    {
        using var db = await SetUpAsync();
        var repository = new StatsRepository(db.Connections);

        Assert.Equal(2, (await repository.LoadGamesAsync()).Count);
    }

    [Theory]
    [InlineData("rapid", 1, 2)]
    [InlineData("blitz", 1, 2)]
    [InlineData("all", 2, 4)]
    public async Task The_home_summary_counts_only_the_selected_time_class(string selection, int games, int moves)
    {
        using var db = await SetUpAsync();

        var summary = await new GameRepository(db.Connections, await ViewAsync(selection)).GetSummaryAsync();

        Assert.Equal((games, moves), (summary.GameCount, summary.MoveCount));
    }

    [Fact]
    public async Task The_summary_dates_follow_the_selection()
    {
        using var db = await SetUpAsync();

        var rapid = await new GameRepository(db.Connections, await ViewAsync("rapid")).GetSummaryAsync();
        var all = await new GameRepository(db.Connections, await ViewAsync("all")).GetSummaryAsync();

        Assert.Equal(("2026-10-10", "2026-10-10"), (rapid.FirstGameDate, rapid.LastGameDate));
        Assert.Equal(("2026-10-10", "2026-10-11"), (all.FirstGameDate, all.LastGameDate));
    }

    [Theory]
    [InlineData("rapid", "r1")]
    [InlineData("blitz", "b1")]
    public async Task The_weekly_focus_uses_only_the_selected_time_class(string selection, string expectedGame)
    {
        using var db = await SetUpAsync();

        var (games, blunders) = await new FocusRepository(db.Connections, await ViewAsync(selection)).LoadAsync();

        Assert.Equal([expectedGame], games.Select(g => g.Id));
        Assert.Equal([expectedGame], blunders.Select(b => b.GameId));
    }

    [Theory]
    [InlineData("rapid", new[] { "r1" })]
    [InlineData("blitz", new[] { "b1" })]
    [InlineData("all", new[] { "r1", "b1" })]
    public async Task An_opponents_page_shows_only_the_games_of_the_selected_time_class(string selection, string[] expected)
    {
        using var db = await SetUpAsync();

        var (games, plies) = await new OpponentRepository(db.Connections, await ViewAsync(selection)).LoadAsync("dana");

        Assert.Equal(expected.Order(), games.Select(g => g.GameId).Order());
        Assert.All(plies, p => Assert.Contains(p.GameId, expected));
    }

    [Theory]
    [InlineData("rapid", 1)]
    [InlineData("blitz", 1)]
    [InlineData("all", 2)]
    public async Task The_opening_report_covers_only_the_selected_time_class(string selection, int expectedGames)
    {
        using var db = await SetUpAsync();

        var games = await new OpeningRepository(db.Connections, await ViewAsync(selection)).LoadAsync();

        Assert.Equal(expectedGames, games.Count);
    }

    [Fact]
    public async Task The_opening_practice_page_covers_only_the_selected_time_class()
    {
        using var db = await SetUpAsync();
        var scope = new ChessCoach.Core.Domain.OpeningScope("white", null, "Italian Game");

        var (rapid, _) = await new OpeningRepository(db.Connections, await ViewAsync("rapid")).LoadVariationAsync(scope);
        var (all, _) = await new OpeningRepository(db.Connections, await ViewAsync("all")).LoadVariationAsync(scope);

        Assert.Equal(["r1"], rapid.Select(g => g.GameId));
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task The_opponent_baseline_follows_the_selected_time_class()
    {
        using var db = await SetUpAsync();

        var rapid = await new OpponentRepository(db.Connections, await ViewAsync("rapid")).LoadBaselineAsync();
        var all = await new OpponentRepository(db.Connections, await ViewAsync("all")).LoadBaselineAsync();

        // each game has two of the player's moves, one of them a blunder
        Assert.Equal(2, rapid.You.Values.Sum(r => r.Moves));
        Assert.Equal(4, all.You.Values.Sum(r => r.Moves));
    }

    [Fact]
    public async Task Setup_and_diagnostics_count_every_game_whatever_is_being_viewed()
    {
        using var db = await SetUpAsync();
        var repository = new GameRepository(db.Connections, await ViewAsync("bullet"));   // no bullet games

        var viewed = await repository.GetSummaryAsync();
        var overall = await repository.GetOverallSummaryAsync();

        Assert.Equal(0, viewed.GameCount);
        Assert.Equal(2, overall.GameCount);
        Assert.Equal(4, overall.MoveCount);
    }
}
