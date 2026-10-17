using ChessCoach.Core.Abstractions;
using ChessCoach.Core.Domain;
using ChessCoach.Core.Training;

namespace ChessCoach.Core.Tests;

public sealed class DrillPlannerTests
{
    private static readonly DateOnly Day = new(2026, 10, 16);

    [Fact]
    public void The_same_day_always_gives_the_same_plan()
    {
        Assert.Equal(DrillPlanner.Plan(Day, 634, 8270), DrillPlanner.Plan(Day, 634, 8270));
    }

    [Fact]
    public void A_plan_has_three_slots_with_distinct_candidates_per_list()
    {
        for (var d = 0; d < 200; d++)
        {
            var plan = DrillPlanner.Plan(Day.AddDays(d), 634, 8270);

            Assert.Equal(DrillPlanner.SlotsPerDay, plan.Count);
            Assert.All(plan.Where(s => s.Format != DrillFormat.Safe), s => Assert.True(s.FromBlunders));
            var keys = plan.Select(s => (s.FromBlunders, s.CandidateIndex)).ToList();
            Assert.Equal(keys.Count, keys.Distinct().Count());
        }
    }

    [Fact]
    public void Over_many_days_every_format_and_both_safe_sources_appear()
    {
        var all = Enumerable.Range(0, 300).SelectMany(d => DrillPlanner.Plan(Day.AddDays(d), 634, 8270)).ToList();

        Assert.Contains(all, s => s.Format == DrillFormat.Hang);
        Assert.Contains(all, s => s.Format == DrillFormat.Cct);
        Assert.Contains(all, s => s.Format == DrillFormat.Safe && s.FromBlunders);
        Assert.Contains(all, s => s.Format == DrillFormat.Safe && !s.FromBlunders);
    }

    [Fact]
    public void Without_blunders_there_is_no_drill_and_without_safe_moves_safe_slots_use_blunders()
    {
        Assert.Empty(DrillPlanner.Plan(Day, 0, 100));
        Assert.All(Enumerable.Range(0, 50).SelectMany(d => DrillPlanner.Plan(Day.AddDays(d), 10, 0)), s => Assert.True(s.FromBlunders));
    }
}

public sealed class DrillGraderTests
{
    [Fact]
    public void Grading_reports_right_missing_and_extra_squares()
    {
        var answer = DrillGrader.Grade(["e4", "D5", "h1"], ["e4", "d5", "c3"]);

        Assert.False(answer.Ok);
        Assert.Equal(["d5", "e4"], answer.Right);
        Assert.Equal(["c3"], answer.Missing);
        Assert.Equal(["h1"], answer.Extra);
    }

    [Fact]
    public void Marking_nothing_when_nothing_is_expected_is_correct()
    {
        Assert.True(DrillGrader.Grade([], []).Ok);
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public void Safe_or_blunder_is_right_when_the_judgement_matches(bool isBlunder, bool choseSafe, bool expected)
    {
        Assert.Equal(expected, DrillGrader.JudgedCorrectly(isBlunder, choseSafe));
    }
}

public sealed class DrillStreakTests
{
    private static readonly DateOnly Today = new(2026, 10, 16);

    [Fact]
    public void Counts_back_from_today_when_today_is_done()
    {
        var days = new HashSet<DateOnly> { Today, Today.AddDays(-1), Today.AddDays(-2), Today.AddDays(-4) };

        Assert.Equal(3, DrillStreak.Compute(days, Today));
    }

    [Fact]
    public void An_unfinished_today_does_not_break_yesterdays_streak()
    {
        var days = new HashSet<DateOnly> { Today.AddDays(-1), Today.AddDays(-2) };

        Assert.Equal(2, DrillStreak.Compute(days, Today));
    }

    [Fact]
    public void A_missed_yesterday_resets_the_streak()
    {
        Assert.Equal(0, DrillStreak.Compute(new HashSet<DateOnly> { Today.AddDays(-2) }, Today));
    }
}

public sealed class DrillServiceTests
{
    private sealed class FakeDrillRepository : IDrillRepository
    {
        public Dictionary<DateOnly, List<DrillSlot>> Days { get; } = [];

        public int Saves { get; private set; }

        public Task<(int Blunders, int Safe)> CountCandidatesAsync(CancellationToken cancellationToken = default) => Task.FromResult((50, 50));

        public Task<DrillPosition?> GetCandidateAsync(bool blunder, int index, CancellationToken cancellationToken = default) =>
            Task.FromResult<DrillPosition?>(new DrillPosition($"{(blunder ? "b" : "s")}_{index}", "fen", "white", "e2e4", "e4", "d4", blunder ? 400 : 5, 0, blunder ? -400 : -5, blunder, "2026-01-01", "opp", "u"));

        public Task<IReadOnlyList<DrillSlot>?> LoadDayAsync(DateOnly day, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DrillSlot>?>(Days.TryGetValue(day, out var s) ? s : null);

        public Task SaveNewDayAsync(DateOnly day, int seed, IReadOnlyList<DrillSlot> slots, CancellationToken cancellationToken = default)
        {
            Days[day] = slots.ToList();
            return Task.CompletedTask;
        }

        public Task SaveSlotAsync(DateOnly day, DrillSlot slot, CancellationToken cancellationToken = default)
        {
            Saves++;
            return Task.CompletedTask;
        }

        public Task<IReadOnlySet<DateOnly>> LoadCompletedDaysAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<DateOnly>>(Days.Where(d => d.Value.All(s => s.Done)).Select(d => d.Key).ToHashSet());
    }

    private static DrillSlot Slot(DrillFormat format, bool blunder = true) => new()
    {
        Index = 0,
        Format = format,
        Position = new DrillPosition("p", "fen", "white", "e2e4", "e4", "d4", blunder ? 400 : 5, 0, 0, blunder, "d", "o", "u"),
    };

    [Fact]
    public async Task Today_is_created_once_and_then_reloaded()
    {
        var repo = new FakeDrillRepository();
        var service = new DrillService(repo, new FakeClock(TestData.Now));

        var first = await service.GetTodayAsync();
        var second = await service.GetTodayAsync();

        Assert.Equal(DrillPlanner.SlotsPerDay, first.Slots.Count);
        Assert.Equal(first.Slots.Select(s => s.Position.PositionId), second.Slots.Select(s => s.Position.PositionId));
        Assert.Single(repo.Days);
    }

    [Fact]
    public async Task A_hang_slot_is_done_after_one_submission()
    {
        var repo = new FakeDrillRepository();
        var service = new DrillService(repo, new FakeClock(TestData.Now));
        var slot = Slot(DrillFormat.Hang);

        var answer = await service.SubmitSquaresAsync(new DateOnly(2026, 10, 16), slot, ["e4"], ["e4"]);

        Assert.True(answer.Ok);
        Assert.True(slot.Done);
        Assert.Equal(1, slot.Score);
        Assert.Equal(1, repo.Saves);
    }

    [Fact]
    public async Task A_cct_slot_takes_three_rounds_and_scores_the_rounds_right()
    {
        var service = new DrillService(new FakeDrillRepository(), new FakeClock(TestData.Now));
        var slot = Slot(DrillFormat.Cct);
        var day = new DateOnly(2026, 10, 16);

        await service.SubmitSquaresAsync(day, slot, ["f7"], ["f7"]);
        Assert.False(slot.Done);
        await service.SubmitSquaresAsync(day, slot, [], ["d5"]);
        await service.SubmitSquaresAsync(day, slot, [], []);

        Assert.True(slot.Done);
        Assert.Equal(3, slot.Round);
        Assert.Equal(2, slot.Score);
        Assert.True(DrillGrader.Passed(slot));
    }

    [Fact]
    public async Task Judging_a_blunder_as_a_blunder_scores_and_completes_the_slot()
    {
        var service = new DrillService(new FakeDrillRepository(), new FakeClock(TestData.Now));
        var slot = Slot(DrillFormat.Safe, blunder: true);

        var ok = await service.JudgeAsync(new DateOnly(2026, 10, 16), slot, choseSafe: false);

        Assert.True(ok);
        Assert.Equal("blunder", slot.Choice);
        Assert.True(slot.Done);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.JudgeAsync(new DateOnly(2026, 10, 16), slot, choseSafe: true));
    }
}
