using ChessCoach.Core.Navigation;

namespace ChessCoach.Core.Tests;

public sealed class PageHistoryTests
{
    private static PageHistory Visited(params string[] pages)
    {
        var history = new PageHistory();
        foreach (var page in pages) history.Arrive(page);
        return history;
    }

    /// <summary>A change to the page being shown, announced and then arrived at, as AppNavigation does it.</summary>
    private static void Change(PageHistory history, string location)
    {
        history.ExpectReplace(location);
        history.Arrive(location);
    }

    [Fact]
    public void A_new_history_has_nowhere_to_go_back_to()
    {
        var history = new PageHistory();

        Assert.False(history.CanGoBack);
        Assert.Null(history.Current);
        Assert.Null(history.Back());
    }

    [Fact]
    public void The_first_page_alone_cannot_be_left_behind()
    {
        var history = Visited("/");

        Assert.False(history.CanGoBack);
        Assert.Null(history.Back());
        Assert.Equal("/", history.Current);
    }

    [Fact]
    public void Back_returns_to_each_earlier_page_in_turn()
    {
        var history = Visited("/", "/study", "/review/1");

        Assert.True(history.CanGoBack);
        Assert.Equal("/study", history.Back());
        Assert.Equal("/", history.Back());
        Assert.Null(history.Back());
        Assert.False(history.CanGoBack);
    }

    [Fact]
    public void Arriving_at_the_same_page_again_adds_nothing()
    {
        var history = Visited("/", "/study", "/study");

        Assert.Equal(2, history.Count);
    }

    [Fact]
    public void An_announced_change_replaces_the_page_instead_of_adding_one()
    {
        var history = Visited("/", "/study");

        Change(history, "/study?tab=2");
        Change(history, "/study?tab=2&color=black");

        Assert.Equal(2, history.Count);
        Assert.Equal("/study?tab=2&color=black", history.Current);
        Assert.Equal("/", history.Back());
    }

    [Fact]
    public void Going_back_from_a_page_returns_to_the_tab_that_was_chosen_there()
    {
        var history = Visited("/", "/study");
        Change(history, "/study?tab=2");
        history.Arrive("/review/7", mayBeBack: false);

        Assert.Equal("/study?tab=2", history.Back());
    }

    [Fact]
    public void An_announced_change_applies_only_to_the_page_it_was_announced_for()
    {
        var history = Visited("/");

        history.ExpectReplace("/study?tab=1");
        history.Arrive("/stats");

        // the announced change never happened: /stats is a visit of its own, and the next visit is not swallowed either
        history.Arrive("/study");

        Assert.Equal(3, history.Count);
        Assert.Equal("/stats", history.Back());
    }

    [Fact]
    public void An_announced_change_that_never_arrives_does_not_swallow_a_later_visit()
    {
        var history = Visited("/", "/study");
        history.ExpectReplace("/study?tab=2");

        history.Arrive("/stats");
        history.Arrive("/settings");

        Assert.Equal(4, history.Count);
        Assert.Equal("/stats", history.Back());
    }

    [Fact]
    public void A_stale_announced_change_does_not_turn_the_browsers_back_into_a_copy_of_the_page()
    {
        var history = Visited("/", "/study");
        history.ExpectReplace("/study?tab=2");

        // the browser goes back to "/" instead
        history.Arrive("/");

        Assert.Equal(1, history.Count);
        Assert.Equal("/", history.Current);
    }

    [Fact]
    public void The_browsers_own_back_arrives_at_the_previous_page_and_counts_as_going_back()
    {
        var history = Visited("/", "/study", "/review/1");

        history.Arrive("/study");

        Assert.Equal(2, history.Count);
        Assert.Equal("/study", history.Current);
        Assert.Equal("/", history.Back());
    }

    [Fact]
    public void Following_a_link_back_to_the_previous_page_is_a_new_visit_not_a_back()
    {
        var history = Visited("/", "/study", "/review/1");

        // a link on the review page to Study: the review page must stay in the history
        history.Arrive("/study", mayBeBack: false);

        Assert.Equal(4, history.Count);
        Assert.Equal("/review/1", history.Back());
        Assert.Equal("/study", history.Back());
    }

    [Fact]
    public void Going_back_with_the_button_then_arriving_there_adds_nothing()
    {
        var history = Visited("/", "/study", "/review/1");

        var previous = history.Back();
        history.Arrive(previous!, mayBeBack: false);

        Assert.Equal(2, history.Count);
        Assert.Equal("/study", history.Current);
    }

    [Fact]
    public void A_pending_announcement_does_not_survive_going_back()
    {
        var history = Visited("/", "/study");
        history.ExpectReplace("/study?tab=1");

        history.Back();
        history.Arrive("/stats");

        Assert.Equal(2, history.Count);
        Assert.Equal("/", history.Back());
    }

    [Fact]
    public void Only_the_most_recent_pages_are_kept()
    {
        var history = new PageHistory(capacity: 3);
        foreach (var page in new[] { "/a", "/b", "/c", "/d", "/e" }) history.Arrive(page);

        Assert.Equal(3, history.Count);
        Assert.Equal("/d", history.Back());
        Assert.Equal("/c", history.Back());
        Assert.Null(history.Back());
    }
}

public sealed class QueryChoiceTests
{
    [Theory]
    [InlineData("0", 4, 0)]
    [InlineData("2", 4, 2)]
    [InlineData("3", 4, 3)]
    [InlineData("4", 4, 0)]
    [InlineData("99", 4, 0)]
    [InlineData("-1", 4, 0)]
    [InlineData("+1", 4, 0)]
    [InlineData(" 1", 4, 0)]
    [InlineData("1.5", 4, 0)]
    [InlineData("abc", 4, 0)]
    [InlineData("", 4, 0)]
    [InlineData(null, 4, 0)]
    [InlineData("99999999999999999999", 4, 0)]
    [InlineData("1", 2, 1)]
    [InlineData("2", 2, 0)]
    public void A_tab_in_the_address_is_used_only_when_it_is_a_real_tab(string? raw, int tabCount, int expected)
    {
        Assert.Equal(expected, QueryChoice.Tab(raw, tabCount));
    }

    [Theory]
    [InlineData("white", "white")]
    [InlineData("black", "black")]
    [InlineData("red", null)]
    [InlineData("White", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void A_colour_in_the_address_is_white_black_or_both(string? raw, string? expected)
    {
        Assert.Equal(expected, QueryChoice.Color(raw));
    }
}
