namespace ChessCoach.Core.Settings;

/// <summary>
/// How the boards are shown: their size (saved) and focus mode (for the page being looked at). The layout listens to
/// <see cref="Changed"/>; every board tells it when it appears and goes, so the header only offers these controls while a board
/// is on screen.
/// </summary>
public sealed class BoardView(BoardSettingsService settings)
{
    /// <summary>
    /// How long a page may be without a board before focus mode ends. Moving from one page with a board to another (or switching a
    /// tab that holds one) removes the old board and adds the new one a moment apart, and that must not end focus mode.
    /// </summary>
    public TimeSpan Grace { get; init; } = TimeSpan.FromMilliseconds(300);

    public int Size { get; private set; } = BoardSizes.Default;

    /// <summary>Focus mode: a bigger board, and only what is needed to use it.</summary>
    public bool FocusMode { get; private set; }

    /// <summary>How many boards are on the page now.</summary>
    public int BoardsOnPage { get; private set; }

    public bool CanGrow => Size < BoardSizes.Max;

    public bool CanShrink => Size > BoardSizes.Min;

    /// <summary>Completes when any pending end of focus mode (after the last board left) has been decided. For tests.</summary>
    public Task Settled { get; private set; } = Task.CompletedTask;

    public event Action? Changed;

    public async Task LoadAsync()
    {
        try
        {
            Size = await settings.GetSizeAsync();
        }
        catch (Exception)
        {
            Size = BoardSizes.Default;   // a size preference is not worth stopping the app for
        }

        Changed?.Invoke();
    }

    public async Task SetSizeAsync(int size)
    {
        Size = BoardSizes.Clamp(size);
        Changed?.Invoke();
        try
        {
            await settings.SetSizeAsync(Size);
        }
        catch (Exception)
        {
            // the board is already the new size; it simply will not be remembered next time
        }
    }

    /// <summary>One step bigger or smaller. At the limit nothing changes and nothing is saved.</summary>
    public Task ChangeSizeAsync(bool larger)
    {
        if (larger ? !CanGrow : !CanShrink) return Task.CompletedTask;
        return SetSizeAsync(BoardSizes.Next(Size, larger));
    }

    public void ToggleFocus()
    {
        FocusMode = !FocusMode;
        Changed?.Invoke();
    }

    public void ExitFocus()
    {
        if (!FocusMode) return;
        FocusMode = false;
        Changed?.Invoke();
    }

    public void BoardAppeared()
    {
        BoardsOnPage++;
        Changed?.Invoke();
    }

    /// <summary>
    /// A board left the page. Focus mode ends once no board has come back within <see cref="Grace"/>, so a page with no board is never
    /// left with its navigation hidden, yet moving between boards keeps focus mode on.
    /// </summary>
    public void BoardGone()
    {
        BoardsOnPage = Math.Max(0, BoardsOnPage - 1);
        Changed?.Invoke();
        if (BoardsOnPage == 0 && FocusMode) Settled = EndFocusIfStillEmptyAsync();
    }

    private async Task EndFocusIfStillEmptyAsync()
    {
        await Task.Delay(Grace);
        if (BoardsOnPage == 0 && FocusMode)
        {
            FocusMode = false;
            Changed?.Invoke();
        }
    }
}
