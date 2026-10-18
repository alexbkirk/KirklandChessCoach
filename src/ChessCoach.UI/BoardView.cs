using ChessCoach.Core.Settings;

namespace ChessCoach.UI;

/// <summary>
/// How the boards are shown: their size (saved) and focus mode (for the page being looked at). The layout listens to
/// <see cref="Changed"/>; every <c>ChessBoard</c> tells it when it appears and goes, so the header only offers these controls
/// while a board is on screen.
/// </summary>
public sealed class BoardView(BoardSettingsService settings)
{
    public int Size { get; private set; } = BoardSizes.Default;

    /// <summary>Focus mode: a bigger board, and only what is needed to use it.</summary>
    public bool FocusMode { get; private set; }

    /// <summary>How many boards are on the page now.</summary>
    public int BoardsOnPage { get; private set; }

    public bool CanGrow => Size < BoardSizes.Max;

    public bool CanShrink => Size > BoardSizes.Min;

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

    public Task ChangeSizeAsync(bool larger) => SetSizeAsync(BoardSizes.Next(Size, larger));

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

    /// <summary>A board left the page. Focus mode ends with the last one, so a page with no board is never left with its navigation hidden.</summary>
    public void BoardGone()
    {
        BoardsOnPage = Math.Max(0, BoardsOnPage - 1);
        if (BoardsOnPage == 0) FocusMode = false;
        Changed?.Invoke();
    }
}
