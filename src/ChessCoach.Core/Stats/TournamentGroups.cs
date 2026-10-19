using ChessCoach.Core.Domain;

namespace ChessCoach.Core.Stats;

/// <summary>One tournament (or over-the-board event) with its games, oldest first, and its prep summary.</summary>
public sealed record TournamentSummary(
    string Key,
    string Name,
    DateOnly Start,
    DateOnly End,
    bool OverTheBoard,
    IReadOnlyList<StatsGame> Games,
    TournamentPrep Prep);

public static class TournamentGroups
{
    private const string OverTheBoardKey = "over-the-board";
    private const string OtbPrefix = "otb:";

    /// <summary>
    /// Display name for a tournament key: a chess.com URL gives its last path segment, an "otb:" key gives its event name,
    /// and the old "over-the-board" value gives "Over-the-board games".
    /// </summary>
    public static string NameOf(string key)
    {
        if (key == OverTheBoardKey)
        {
            return "Over-the-board games";
        }

        if (key.StartsWith(OtbPrefix, StringComparison.Ordinal))
        {
            var eventName = key[OtbPrefix.Length..].Trim();
            return eventName.Length == 0 ? "Unnamed event" : eventName;
        }

        if (Uri.TryCreate(key, UriKind.Absolute, out var uri))
        {
            var path = uri.AbsolutePath.Trim('/');
            var last = path[(path.LastIndexOf('/') + 1)..];
            if (last.Trim().Length > 0)
            {
                var words = last.Replace('-', ' ').Trim();
                return char.ToUpperInvariant(words[0]) + words[1..];
            }
        }

        return key;
    }

    /// <summary>
    /// Groups games by their tournament key. Games with no key are left out. Games inside a group are oldest first;
    /// groups are newest first by End, then by Name.
    /// </summary>
    public static IReadOnlyList<TournamentSummary> Group(
        IReadOnlyList<StatsGame> games,
        IReadOnlyDictionary<string, string> keyByGameId,
        IReadOnlyList<MoveSample> moves)
    {
        var summaries = games
            .Where(g => keyByGameId.TryGetValue(g.Id, out var key) && !string.IsNullOrWhiteSpace(key))
            .GroupBy(g => keyByGameId[g.Id])
            .Select(group =>
            {
                var ordered = group
                    .OrderBy(g => g.Date)
                    .ThenBy(g => g.Time, StringComparer.Ordinal)
                    .ToList();

                return new TournamentSummary(
                    Key: group.Key,
                    Name: NameOf(group.Key),
                    Start: ordered.Min(g => g.Date),
                    End: ordered.Max(g => g.Date),
                    OverTheBoard: !group.Key.StartsWith("http", StringComparison.Ordinal),
                    Games: ordered,
                    Prep: TournamentPrepCalculator.Compute(ordered, moves));
            })
            .OrderByDescending(t => t.End)
            .ThenBy(t => t.Name, StringComparer.Ordinal)
            .ToList();

        return summaries;
    }
}
