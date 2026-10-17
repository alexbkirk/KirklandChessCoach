using System.Text.Json;
using System.Text.Json.Serialization;
using ChessCoach.Core.Domain;

namespace ChessCoach.Infrastructure.Data;

/// <summary>
/// The subset of a note's JSON that the app uses. Coach notes (analysis/coach.py, table coach_notes) and drill notes
/// (analysis/explain_blunders.py, table blunder_notes) share this shape; drill notes have no Maia, pattern or accept list.
/// </summary>
internal sealed class NoteJson
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public string? Headline { get; set; }

    public List<string>? Why { get; set; }

    public string? Better { get; set; }

    [JsonPropertyName("better_line")]
    public string? BetterLine { get; set; }

    public string? Side { get; set; }

    public MotifJson? Motif { get; set; }

    public string? Pattern { get; set; }

    public MaiaJson? Maia { get; set; }

    [JsonPropertyName("eval_before")]
    public string? EvalBefore { get; set; }

    [JsonPropertyName("eval_after")]
    public string? EvalAfter { get; set; }

    public List<string>? Accept { get; set; }

    public static NoteJson? Parse(string json) => JsonSerializer.Deserialize<NoteJson>(json, JsonOptions);

    public CoachNote ToCoachNote() => new(
        Headline: Headline ?? "",
        Why: Why ?? [],
        BetterSan: Better ?? "",
        BetterLine: BetterLine ?? "",
        Motif: MotifText(),
        Pattern: Pattern ?? "",
        MaiaText: Maia?.Text ?? "",
        EvalBefore: EvalBefore ?? "",
        EvalAfter: EvalAfter ?? "");

    /// <summary>"Knight fork allowed", "Missed pin", or the bare label for what your own move gave away (hung piece, moved defender, ignored threat, lost piece).</summary>
    private string MotifText()
    {
        var label = Motif?.L;
        if (string.IsNullOrEmpty(label)) return "";
        if (Motif?.K is "hung_piece" or "moved_defender" or "ignored_threat" or "lost_piece") return label;
        return Side == "missed" ? $"Missed {char.ToLowerInvariant(label[0])}{label[1..]}" : $"{label} allowed";
    }

    internal sealed class MotifJson
    {
        public string? K { get; set; }

        public string? L { get; set; }
    }

    internal sealed class MaiaJson
    {
        public string? Text { get; set; }
    }
}
