using ChessCoach.Core.Domain;

namespace ChessCoach.Core.Training;

/// <summary>
/// Groups the tactic named in a blunder's note (analysis/motifs.py) into a handful of kinds a player can work on.
/// A note says whether the move <c>allowed</c> the opponent a tactic or <c>missed</c> one of yours.
/// </summary>
public static class LeakCatalog
{
    public static readonly LeakInfo IgnoredThreat = new("ignored_threat", "Ignored threats",
        "A piece of yours was already under attack, and the move left it there.",
        "Before each move, ask what the opponent's last move attacks. Deal with that first.");

    public static readonly LeakInfo HungPiece = new("hung_piece", "Hung pieces",
        "The piece you moved could be taken for free, or by something cheaper, on its new square.",
        "Before you let go, look at the square your piece lands on: who can take it, and who defends it?");

    public static readonly LeakInfo MovedDefender = new("moved_defender", "Moved defenders",
        "The piece you moved was guarding another one, which was then taken.",
        "Before moving a piece, check what it was protecting.");

    public static readonly LeakInfo LoosePiece = new("loose_piece", "Pieces left unprotected",
        "Your move left a different piece of yours undefended, and the opponent took it.",
        "After choosing a move, scan your pieces for any that are attacked and undefended.");

    public static readonly LeakInfo ForkAllowed = new("fork", "Forks allowed",
        "The move let the opponent attack two things at once.",
        "Look at the opponent's checks and knight jumps after your move, especially near your king and queen.");

    public static readonly LeakInfo LineAllowed = new("line", "Pins, skewers and discovered attacks allowed",
        "The move lined your pieces up for a pin, a skewer or a discovered attack.",
        "Watch for your king, queen and rooks standing on one line or diagonal with an enemy bishop, rook or queen on it.");

    public static readonly LeakInfo MateAllowed = new("mate", "Mates allowed",
        "The move let the opponent force checkmate.",
        "Before each move, look at every check the opponent could give next.");

    public static readonly LeakInfo MissedMate = new("missed_mate", "Missed mates",
        "You had a forced checkmate and played something else.",
        "When the opponent's king is short of squares, look at every check you have before anything else.");

    public static readonly LeakInfo MissedWin = new("missed_win", "Missed wins of material",
        "You had a tactic that won material and played something else.",
        "Before a quiet move, look at your checks, captures and threats first.");

    public static readonly LeakInfo Other = new("other", "No single tactic",
        "The move made the position much worse without one named tactic behind it.",
        "");

    public static IReadOnlyList<LeakInfo> All { get; } =
        [IgnoredThreat, HungPiece, MovedDefender, LoosePiece, ForkAllowed, LineAllowed, MateAllowed, MissedMate, MissedWin, Other];

    /// <summary>The kind for a note's side and motif; <see cref="Other"/> when there is no named tactic.</summary>
    public static LeakInfo For(string? side, string? motif) => (side, motif) switch
    {
        ("allowed", "ignored_threat") => IgnoredThreat,
        ("allowed", "hung_piece") => HungPiece,
        ("allowed", "moved_defender") => MovedDefender,
        ("allowed", "lost_piece" or "free_piece" or "removing_the_defender" or "promotion") => LoosePiece,
        ("allowed", "fork" or "double_check") => ForkAllowed,
        ("allowed", "pin" or "skewer" or "discovered_attack" or "discovered_check") => LineAllowed,
        ("allowed", "mate" or "back_rank_mate" or "smothered_mate") => MateAllowed,
        ("missed", "mate" or "back_rank_mate" or "smothered_mate") => MissedMate,
        ("missed", not null) => MissedWin,
        _ => Other,
    };

    public static LeakInfo? Find(string key) => All.FirstOrDefault(l => l.Key == key);
}
