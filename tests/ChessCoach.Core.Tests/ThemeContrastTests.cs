using ChessCoach.Core.Settings;
using ChessCoach.UI;
using MudBlazor;
using MudBlazor.Utilities;

namespace ChessCoach.Core.Tests;

/// <summary>
/// Every theme's colours meet WCAG 2.1 AA for contrast: 4.5:1 for text and 3:1 for the edges and colours that identify a control
/// (focus rings, input and button outlines). A new or changed palette that slips below fails here.
/// </summary>
public sealed class ThemeContrastTests
{
    private const double Text = 4.5;
    private const double Component = 3.0;

    private static double Luminance(string hex)
    {
        var h = hex.TrimStart('#');
        double Channel(int i)
        {
            var v = Convert.ToInt32(h.Substring(i, 2), 16) / 255.0;
            return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(0) + 0.7152 * Channel(2) + 0.0722 * Channel(4);
    }

    private static double Ratio(string a, string b)
    {
        var (hi, lo) = (Luminance(a), Luminance(b));
        if (hi < lo) (hi, lo) = (lo, hi);
        return (hi + 0.05) / (lo + 0.05);
    }

    private static string Hex(MudColor color) => color.ToString(MudColorOutputFormats.Hex);

    /// <summary>The colours the app draws with for one theme, whichever palette (light or dark) it uses.</summary>
    private sealed record Colours(
        string Background, string Surface, string Text, string Muted, string Primary, string OnPrimary, string Secondary, string OnSecondary,
        string Edge, (string Name, string Colour, string OnColour)[] Semantic);

    private static Colours For(string id)
    {
        var theme = ThemePalettes.For(id);
        if (Themes.Find(id).Dark)
        {
            var p = theme.PaletteDark;
            return new Colours(Hex(p.Background), Hex(p.Surface), Hex(p.TextPrimary), Hex(p.TextSecondary), Hex(p.Primary), Hex(p.PrimaryContrastText),
                Hex(p.Secondary), Hex(p.SecondaryContrastText), Hex(p.LinesInputs),
                [("Success", Hex(p.Success), Hex(p.SuccessContrastText)), ("Warning", Hex(p.Warning), Hex(p.WarningContrastText)),
                 ("Error", Hex(p.Error), Hex(p.ErrorContrastText)), ("Info", Hex(p.Info), Hex(p.InfoContrastText))]);
        }

        var l = theme.PaletteLight;
        return new Colours(Hex(l.Background), Hex(l.Surface), Hex(l.TextPrimary), Hex(l.TextSecondary), Hex(l.Primary), Hex(l.PrimaryContrastText),
            Hex(l.Secondary), Hex(l.SecondaryContrastText), Hex(l.LinesInputs),
            [("Success", Hex(l.Success), Hex(l.SuccessContrastText)), ("Warning", Hex(l.Warning), Hex(l.WarningContrastText)),
             ("Error", Hex(l.Error), Hex(l.ErrorContrastText)), ("Info", Hex(l.Info), Hex(l.InfoContrastText))]);
    }

    public static TheoryData<string> AllThemes() => [.. Themes.All.Select(t => t.Id)];

    private static void AtLeast(double needed, string what, string foreground, string background)
    {
        var ratio = Ratio(foreground, background);
        Assert.True(ratio >= needed, $"{what}: {foreground} on {background} is {ratio:0.00}:1, needs {needed}:1");
    }

    [Theory]
    [MemberData(nameof(AllThemes))]
    public void Body_text_is_readable_on_the_page_and_on_cards(string id)
    {
        var c = For(id);
        AtLeast(Text, "text on the page", c.Text, c.Background);
        AtLeast(Text, "text on a card", c.Text, c.Surface);
        AtLeast(Text, "muted text on the page", c.Muted, c.Background);
        AtLeast(Text, "muted text on a card", c.Muted, c.Surface);
    }

    [Theory]
    [MemberData(nameof(AllThemes))]
    public void The_accent_colour_works_as_link_text_and_the_button_text_on_it_is_readable(string id)
    {
        var c = For(id);
        AtLeast(Text, "accent as link text on the page", c.Primary, c.Background);
        AtLeast(Text, "accent as link text on a card", c.Primary, c.Surface);
        AtLeast(Text, "text on an accent button", c.OnPrimary, c.Primary);
    }

    [Theory]
    [MemberData(nameof(AllThemes))]
    public void Chips_and_filled_buttons_in_the_second_colour_are_readable(string id)
    {
        var c = For(id);
        AtLeast(Text, "text on a secondary chip", c.OnSecondary, c.Secondary);
    }

    [Theory]
    [MemberData(nameof(AllThemes))]
    public void Success_warning_error_and_info_chips_have_readable_text(string id)
    {
        var c = For(id);
        foreach (var (name, colour, onColour) in c.Semantic)
        {
            AtLeast(Text, $"text on a {name} chip", onColour, colour);
        }
    }

    [Theory]
    [MemberData(nameof(AllThemes))]
    public void Success_warning_error_and_info_colours_can_be_used_as_text_and_as_outlines_on_the_page(string id)
    {
        var c = For(id);
        foreach (var (name, colour, _) in c.Semantic)
        {
            AtLeast(Text, $"{name} text on the page", colour, c.Background);
            AtLeast(Text, $"{name} text on a card", colour, c.Surface);
        }

        AtLeast(Component, "secondary outline on the page", c.Secondary, c.Background);
        AtLeast(Component, "secondary outline on a card", c.Secondary, c.Surface);
    }

    [Theory]
    [MemberData(nameof(AllThemes))]
    public void Input_and_button_outlines_and_the_focus_ring_stand_out_from_the_page(string id)
    {
        var c = For(id);
        AtLeast(Component, "outline on the page", c.Edge, c.Background);
        AtLeast(Component, "outline on a card", c.Edge, c.Surface);
        AtLeast(Component, "focus ring on the page", c.Primary, c.Background);
        AtLeast(Component, "focus ring on a card", c.Primary, c.Surface);
    }
}
