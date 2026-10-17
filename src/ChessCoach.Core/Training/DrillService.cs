using ChessCoach.Core.Abstractions;
using ChessCoach.Core.Domain;

namespace ChessCoach.Core.Training;

/// <summary>
/// Use-case layer for the daily blunder-check drill. The expected squares for a position come from the UI
/// (chess rules run in the browser via chess.js); this class grades, scores and persists.
/// </summary>
public sealed class DrillService(IDrillRepository repository, IClock clock)
{
    public async Task<DrillDay> GetTodayAsync(CancellationToken cancellationToken = default)
    {
        var day = clock.LocalToday;
        var slots = await repository.LoadDayAsync(day, cancellationToken) ?? await CreateDayAsync(day, cancellationToken);
        var completed = await repository.LoadCompletedDaysAsync(cancellationToken);
        return new DrillDay(day, slots, DrillStreak.Compute(completed, day), completed.Count);
    }

    /// <summary>Grades marked squares for a Hang slot, or for the current round of a Cct slot.</summary>
    public async Task<DrillAnswer> SubmitSquaresAsync(DateOnly day, DrillSlot slot, IReadOnlyCollection<string> selected, IReadOnlyCollection<string> expected, CancellationToken cancellationToken = default)
    {
        if (slot.Done || slot.Format == DrillFormat.Safe)
        {
            throw new InvalidOperationException($"Slot {slot.Index} does not take square answers now.");
        }

        var answer = DrillGrader.Grade(selected, expected);
        slot.Answers.Add(answer);
        if (slot.Format == DrillFormat.Hang)
        {
            slot.Score = answer.Ok ? 1 : 0;
            slot.Done = true;
        }
        else
        {
            slot.Score += answer.Ok ? 1 : 0;
            slot.Round++;
            slot.Done = slot.Round >= DrillGrader.CctRounds.Length;
        }

        await repository.SaveSlotAsync(day, slot, cancellationToken);
        return answer;
    }

    /// <summary>Records a safe-or-blunder judgement.</summary>
    public async Task<bool> JudgeAsync(DateOnly day, DrillSlot slot, bool choseSafe, CancellationToken cancellationToken = default)
    {
        if (slot.Done || slot.Format != DrillFormat.Safe)
        {
            throw new InvalidOperationException($"Slot {slot.Index} does not take a safe/blunder judgement now.");
        }

        var ok = DrillGrader.JudgedCorrectly(slot.Position.IsBlunder, choseSafe);
        slot.Choice = choseSafe ? "safe" : "blunder";
        slot.Answers.Add(new DrillAnswer(ok, [], [], []));
        slot.Score = ok ? 1 : 0;
        slot.Done = true;
        await repository.SaveSlotAsync(day, slot, cancellationToken);
        return ok;
    }

    private async Task<IReadOnlyList<DrillSlot>> CreateDayAsync(DateOnly day, CancellationToken cancellationToken)
    {
        var (blunders, safe) = await repository.CountCandidatesAsync(cancellationToken);
        var slots = new List<DrillSlot>();
        foreach (var planned in DrillPlanner.Plan(day, blunders, safe))
        {
            var position = await repository.GetCandidateAsync(planned.FromBlunders, planned.CandidateIndex, cancellationToken);
            if (position is not null)
            {
                slots.Add(new DrillSlot { Index = slots.Count, Format = planned.Format, Position = position });
            }
        }

        if (slots.Count > 0)
        {
            await repository.SaveNewDayAsync(day, DrillPlanner.SeedFor(day), slots, cancellationToken);
        }

        return slots;
    }
}
