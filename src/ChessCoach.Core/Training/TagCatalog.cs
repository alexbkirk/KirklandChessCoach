namespace ChessCoach.Core.Training;

/// <summary>What a blunder tag means and how to coach it. Keys match analysis/tagging.py.</summary>
public sealed record TagInfo(string Key, string Label, string Title, string Description, string Advice);

public static class TagCatalog
{
    /// <summary>Positional slips are shown in the leak table but never chosen as the focus.</summary>
    public const string Other = "other";

    public static readonly IReadOnlyList<TagInfo> All =
    [
        new("missed_mate", "Missed mate", "Missed mates",
            "You had a forced checkmate and played something else.",
            "Before each move, name every check you can give, then every capture. Follow each forcing line to the end: mates are usually hidden in check-check-mate sequences."),
        new("allowed_mate", "Allowed mate", "Allowing mates",
            "Your move gave the opponent a forced checkmate.",
            "Before each move, ask what checks your opponent will have afterwards. Count your king's escape squares before you push pawns or move defenders away."),
        new("one_move", "One-move blunder", "One-move blunders",
            "You lost material (a minor piece or more) within the opponent's next two moves.",
            "Do a blunder check before you release the piece: look at the destination square and ask what your move stops protecting. Is anything hanging?"),
        new("missed_tactic", "Missed tactic", "Missed tactics",
            "A check, mate or material-winning shot was there and you played something else.",
            "Before any quiet move, look at checks, captures and threats for BOTH sides. Spend your time on the forcing moves first."),
        new("missed_capture", "Missed capture", "Missed captures",
            "A free piece was on the board and you did not take it.",
            "On every move, scan for undefended or loose pieces first. A free piece is worth more than any plan."),
        new(Other, "Positional slip", "Positional slips",
            "No immediate material or mate consequence; the position simply got worse.",
            "Compare your move with your opponent's strongest reply and prefer the move that keeps your pieces protected and your king safe."),
    ];

    public static TagInfo? Find(string key) => All.FirstOrDefault(t => t.Key == key);
}
