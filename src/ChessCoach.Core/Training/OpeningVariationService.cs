using ChessCoach.Core.Abstractions;
using ChessCoach.Core.Domain;

namespace ChessCoach.Core.Training;

/// <summary>The variation page: review your usual line and practice the positions where you go wrong.</summary>
public sealed class OpeningVariationService(IOpeningRepository repository, IClock clock)
{
    public async Task<VariationReview> GetAsync(OpeningScope scope, CancellationToken cancellationToken = default)
    {
        var (games, plies) = await repository.LoadVariationAsync(scope, cancellationToken);
        return OpeningVariationCalculator.Compute(scope, games, plies);
    }

    public Task<IReadOnlyDictionary<string, PracticeTally>> GetPracticeAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default) =>
        repository.LoadPracticeAsync(keys, cancellationToken);

    /// <summary>Grades a practice move and records it. Returns whether it was right.</summary>
    public async Task<bool> AnswerAsync(TroubleSpot spot, string uci, CancellationToken cancellationToken = default)
    {
        var correct = OpeningVariationCalculator.IsCorrect(spot, uci);
        await repository.RecordPracticeAsync(spot.Key, uci, correct, clock.UtcNow, cancellationToken);
        return correct;
    }
}
