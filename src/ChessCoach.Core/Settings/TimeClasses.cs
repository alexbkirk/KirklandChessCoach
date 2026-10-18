using ChessCoach.Core.Abstractions;

namespace ChessCoach.Core.Settings;

/// <summary>chess.com's time classes the app can hold: rapid always, blitz and bullet when the player turns them on.</summary>
public static class TimeClasses
{
    public const string Rapid = "rapid";
    public const string Blitz = "blitz";
    public const string Bullet = "bullet";

    /// <summary>The view that shows every time class held.</summary>
    public const string All = "all";

    /// <summary>The classes that can be imported, longest first. Rapid is always imported.</summary>
    public static IReadOnlyList<string> Importable { get; } = [Rapid, Blitz, Bullet];

    public static string Label(string timeClass) => timeClass switch
    {
        Rapid => "Rapid",
        Blitz => "Blitz",
        Bullet => "Bullet",
        All => "All games",
        _ => timeClass,
    };

    /// <summary>The valid classes in <paramref name="classes"/>, once each, in the usual order, always with rapid.</summary>
    public static IReadOnlyList<string> Normalize(IEnumerable<string>? classes)
    {
        var wanted = new HashSet<string>(classes ?? [], StringComparer.Ordinal) { Rapid };
        return Importable.Where(wanted.Contains).ToList();
    }

    public static string Join(IEnumerable<string> classes) => string.Join(',', Normalize(classes));

    /// <summary>Reads a saved comma-separated list. Anything unreadable means rapid only.</summary>
    public static IReadOnlyList<string> Parse(string? saved) =>
        Normalize(saved?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}

/// <summary>What the next import should fetch: the classes, and whether older months must be searched again for newly added ones.</summary>
public sealed record ImportPlan(IReadOnlyList<string> Classes, bool Backfill);

/// <summary>Loads and saves which time classes are imported and which one the app is showing.</summary>
public sealed class TimeClassSettings(IAppSettingsStore store)
{
    public const string ImportKey = "import_time_classes";
    public const string BackfillKey = "import_backfill";
    public const string ViewKey = "view_time_class";

    public async Task<IReadOnlyList<string>> GetImportClassesAsync(CancellationToken cancellationToken = default) =>
        TimeClasses.Parse(await store.GetAsync(ImportKey, cancellationToken));

    public async Task<ImportPlan> GetImportPlanAsync(CancellationToken cancellationToken = default) =>
        new(await GetImportClassesAsync(cancellationToken), await store.GetAsync(BackfillKey, cancellationToken) == "1");

    /// <summary>
    /// Saves which classes to import. A class added since the last save means the next import must go back through every month
    /// (a normal import only looks from the latest stored game onward), so that is remembered until an import finishes.
    /// </summary>
    public async Task<IReadOnlyList<string>> SetImportClassesAsync(IEnumerable<string> classes, CancellationToken cancellationToken = default)
    {
        var before = await GetImportClassesAsync(cancellationToken);
        var after = TimeClasses.Normalize(classes);
        await store.SetAsync(ImportKey, TimeClasses.Join(after), cancellationToken);
        if (after.Except(before).Any()) await store.SetAsync(BackfillKey, "1", cancellationToken);
        else if (after.Count == 1) await ClearBackfillAsync(cancellationToken);   // only rapid left: nothing new to search for
        return after;
    }

    public Task ClearBackfillAsync(CancellationToken cancellationToken = default) => store.SetAsync(BackfillKey, "0", cancellationToken);

    /// <summary>The class being shown: "all", or one of the imported classes; rapid when the saved value is missing or no longer imported.</summary>
    public async Task<string> GetViewAsync(CancellationToken cancellationToken = default)
    {
        var saved = await store.GetAsync(ViewKey, cancellationToken);
        var imported = await GetImportClassesAsync(cancellationToken);
        return saved == TimeClasses.All || (saved is not null && imported.Contains(saved)) ? saved! : TimeClasses.Rapid;
    }

    public Task SetViewAsync(string view, CancellationToken cancellationToken = default) =>
        store.SetAsync(ViewKey, view == TimeClasses.All || TimeClasses.Importable.Contains(view) ? view : TimeClasses.Rapid, cancellationToken);
}

/// <summary>
/// The time class every page is showing right now, for the repositories to filter by. The layout listens to <see cref="Changed"/>
/// and rebuilds the page, so every page shows the new class.
/// </summary>
public sealed class TimeClassView(TimeClassSettings settings)
{
    /// <summary>The class to show, or null for all of them.</summary>
    public string? Current { get; private set; } = TimeClasses.Rapid;

    public IReadOnlyList<string> Imported { get; private set; } = [TimeClasses.Rapid];

    /// <summary>"rapid", "blitz", "bullet" or "all".</summary>
    public string Selection => Current ?? TimeClasses.All;

    public event Action? Changed;

    public async Task LoadAsync()
    {
        try
        {
            Imported = await settings.GetImportClassesAsync();
            var view = await settings.GetViewAsync();
            Current = view == TimeClasses.All ? null : view;
        }
        catch (Exception)
        {
            Imported = [TimeClasses.Rapid];   // a view preference is not worth stopping the app for
            Current = TimeClasses.Rapid;
        }

        Changed?.Invoke();
    }

    public async Task SelectAsync(string view)
    {
        var valid = view == TimeClasses.All || Imported.Contains(view) ? view : TimeClasses.Rapid;
        Current = valid == TimeClasses.All ? null : valid;
        Changed?.Invoke();
        try
        {
            await settings.SetViewAsync(valid);
        }
        catch (Exception)
        {
            // shown now, just not remembered next time
        }
    }

    /// <summary>Changes which classes are imported. If the class being viewed is no longer imported, the view goes back to rapid.</summary>
    public async Task SetImportedAsync(IEnumerable<string> classes)
    {
        Imported = await settings.SetImportClassesAsync(classes);
        if (Current is not null && !Imported.Contains(Current)) await SelectAsync(TimeClasses.Rapid);
        else Changed?.Invoke();
    }
}
