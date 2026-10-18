namespace ChessCoach.Core.Setup;

/// <summary>Progress of one engine download: the stage ("Downloading" or "Checked"), and bytes so far out of the expected total.</summary>
public sealed record EngineProgress(string Engine, string Stage, long Done, long Total);
