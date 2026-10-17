using Microsoft.JSInterop;

namespace ChessCoach.UI;

/// <summary>The answers the daily drill needs for a position (computed with chess.js in the browser).</summary>
public sealed record DrillAnswers(string FenAfter, string[] Checks, string[] Captures, string[] Threats, string[] Hanging);

/// <summary>Chess rules from chess.js (wwwroot/js/chess-rules.js); the C# side has no chess library.</summary>
public sealed class ChessRules(IJSRuntime js) : IAsyncDisposable
{
    private IJSObjectReference? _module;

    public async Task<DrillAnswers> DrillAnswersAsync(string fen, string uci)
    {
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./_content/ChessCoach.UI/js/chess-rules.js");
        return await _module.InvokeAsync<DrillAnswers>("drillAnswers", fen, uci);
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null) await _module.DisposeAsync();
    }
}
