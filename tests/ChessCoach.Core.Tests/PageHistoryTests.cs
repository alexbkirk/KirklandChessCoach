using ChessCoach.Core.Navigation;

namespace ChessCoach.Core.Tests;

public sealed class PageHistoryTests
{
    private static PageHistory Visited(params string[] pages)
    {
        var stack = new PageHistory();
        foreach (var page in pages) stack.Arrive(page);
        return stack;
    }

    [Fact]
    public void A_new_stack_has_nowhere_to_go_back_to()
    {
        var stack = new PageHistory();

        Assert.False(stack.CanGoBack);
        Assert.Null(stack.Current);
        Assert.Null(stack.Back());
    }

    [Fact]
    public void The_first_page_alone_cannot_be_left_behind()
    {
        var stack = Visited("/");

        Assert.False(stack.CanGoBack);
        Assert.Null(stack.Back());
        Assert.Equal("/", stack.Current);
    }

    [Fact]
    public void Back_returns_to_each_earlier_page_in_turn()
    {
        var stack = Visited("/", "/study", "/review/1");

        Assert.True(stack.CanGoBack);
        Assert.Equal("/study", stack.Back());
        Assert.Equal("/", stack.Back());
        Assert.Null(stack.Back());
        Assert.False(stack.CanGoBack);
    }

    [Fact]
    public void Arriving_at_the_same_page_again_adds_nothing()
    {
        var stack = Visited("/", "/study", "/study");

        Assert.Equal(2, stack.Count);
    }

    [Fact]
    public void A_marked_change_replaces_the_page_instead_of_adding_one()
    {
        var stack = Visited("/", "/study");

        stack.ReplaceNext();
        stack.Arrive("/study?tab=2");
        stack.ReplaceNext();
        stack.Arrive("/study?tab=2&color=black");

        Assert.Equal(2, stack.Count);
        Assert.Equal("/study?tab=2&color=black", stack.Current);
        Assert.Equal("/", stack.Back());
    }

    [Fact]
    public void Going_back_from_a_page_returns_to_the_tab_that_was_chosen_there()
    {
        var stack = Visited("/", "/study");
        stack.ReplaceNext();
        stack.Arrive("/study?tab=2");
        stack.Arrive("/review/7");

        Assert.Equal("/study?tab=2", stack.Back());
    }

    [Fact]
    public void A_marked_change_is_used_once()
    {
        var stack = Visited("/");

        stack.ReplaceNext();
        stack.Arrive("/study");
        stack.Arrive("/stats");

        Assert.Equal(2, stack.Count);
        Assert.Equal("/study", stack.Back());
    }

    [Fact]
    public void The_browsers_own_back_arrives_at_the_previous_page_and_counts_as_going_back()
    {
        var stack = Visited("/", "/study", "/review/1");

        stack.Arrive("/study");

        Assert.Equal(2, stack.Count);
        Assert.Equal("/study", stack.Current);
        Assert.Equal("/", stack.Back());
    }

    [Fact]
    public void Going_back_with_the_button_then_arriving_there_adds_nothing()
    {
        var stack = Visited("/", "/study", "/review/1");

        var previous = stack.Back();
        stack.Arrive(previous!);

        Assert.Equal(2, stack.Count);
        Assert.Equal("/study", stack.Current);
    }

    [Fact]
    public void A_pending_change_mark_does_not_survive_going_back()
    {
        var stack = Visited("/", "/study");
        stack.ReplaceNext();

        stack.Back();
        stack.Arrive("/stats");

        Assert.Equal(2, stack.Count);
        Assert.Equal("/", stack.Back());
    }

    [Fact]
    public void Only_the_most_recent_pages_are_kept()
    {
        var stack = new PageHistory(capacity: 3);
        foreach (var page in new[] { "/a", "/b", "/c", "/d", "/e" }) stack.Arrive(page);

        Assert.Equal(3, stack.Count);
        Assert.Equal("/d", stack.Back());
        Assert.Equal("/c", stack.Back());
        Assert.Null(stack.Back());
    }
}
