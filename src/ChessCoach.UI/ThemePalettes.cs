using ChessCoach.Core.Settings;
using MudBlazor;

namespace ChessCoach.UI;

/// <summary>The colours and fonts for each theme in <see cref="Themes"/>. Board colours are CSS variables set per theme in app.css.</summary>
public static class ThemePalettes
{
    private static readonly string[] Serif = ["Georgia", "Cambria", "serif"];
    private static readonly string[] Sans = ["Segoe UI", "Roboto", "system-ui", "sans-serif"];

    /// <summary>A swatch for the theme picker: background, surface, accent and text.</summary>
    public static (string Background, string Surface, string Accent, string Text) Swatch(string id) => id switch
    {
        Themes.Evergreen => ("#0D1512", "#13201B", "#8BC98A", "#DDE8E1"),
        Themes.Birch => ("#F3EFE6", "#FBF9F4", "#4F6F4A", "#2B2A26"),
        _ => ("#1C1510", "#241B15", "#C9A15E", "#EFE4D2"),
    };

    public static MudTheme For(string id)
    {
        var dark = Themes.Find(id).Dark;
        var palette = id switch
        {
            Themes.Evergreen => Dark(background: "#0D1512", surface: "#13201B", primary: "#8BC98A", secondary: "#E39A5B", text: "#DDE8E1", muted: "#8EA79A", divider: "#22362D", edge: "#6B8478", onPrimary: "#0D1512"),
            Themes.Birch => Light(),
            _ => Dark(background: "#1C1510", surface: "#241B15", primary: "#C9A15E", secondary: "#B5703F", text: "#EFE4D2", muted: "#BFAE97", divider: "#3A2C21", edge: "#8A7A68", onPrimary: "#241B15"),
        };

        return new MudTheme
        {
            PaletteLight = dark ? new PaletteLight() : palette.Light,
            PaletteDark = dark ? palette.Dark : new PaletteDark(),
            LayoutProperties = new LayoutProperties { DefaultBorderRadius = "14px" },
            Typography = Typography(id == Themes.Evergreen ? Sans : Serif),
        };
    }

    private static MudBlazor.Typography Typography(string[] headingFont) => new()
    {
        Default = new DefaultTypography { FontFamily = Sans },
        H3 = new H3Typography { FontFamily = headingFont, FontWeight = "500" },
        H4 = new H4Typography { FontFamily = headingFont, FontWeight = "500" },
        H5 = new H5Typography { FontFamily = headingFont, FontWeight = "500" },
        H6 = new H6Typography { FontFamily = headingFont, FontWeight = "600" },
        Button = new ButtonTypography { TextTransform = "none", FontWeight = "600" },
    };

    private static (PaletteLight Light, PaletteDark Dark) Dark(string background, string surface, string primary, string secondary, string text, string muted, string divider, string edge, string onPrimary) =>
        (new PaletteLight(), new PaletteDark
        {
            Primary = primary,
            PrimaryContrastText = onPrimary,
            Secondary = secondary,
            Background = background,
            BackgroundGray = surface,
            Surface = surface,
            AppbarBackground = surface,
            AppbarText = text,
            DrawerBackground = surface,
            DrawerText = text,
            TextPrimary = text,
            TextSecondary = muted,
            TextDisabled = muted,
            ActionDefault = muted,
            Divider = divider,
            DividerLight = divider,
            LinesDefault = divider,
            LinesInputs = edge,
            TableLines = divider,
            Success = "#5FB47F",
            Warning = "#E0A84C",
            Error = "#E0705A",
            Info = "#6FA8C7",
            // dark text on these lighter colours (white on them was 2.1 to 3.2:1; these are 5.7 to 8.7:1)
            SuccessContrastText = background,
            WarningContrastText = background,
            ErrorContrastText = background,
            InfoContrastText = background,
            SecondaryContrastText = background,
        });

    private static (PaletteLight Light, PaletteDark Dark) Light() =>
        (new PaletteLight
        {
            Primary = "#4F6F4A",
            PrimaryContrastText = "#FBF9F4",
            Secondary = "#8A5E37",
            SecondaryContrastText = "#FBF9F4",
            Background = "#F3EFE6",
            BackgroundGray = "#ECE6D8",
            Surface = "#FBF9F4",
            AppbarBackground = "#FBF9F4",
            AppbarText = "#2B2A26",
            DrawerBackground = "#FBF9F4",
            DrawerText = "#2B2A26",
            TextPrimary = "#2B2A26",
            TextSecondary = "#5C584F",
            TextDisabled = "#8F8A7E",
            ActionDefault = "#5C584F",
            Divider = "#E2DACB",
            DividerLight = "#E2DACB",
            LinesDefault = "#E2DACB",
            LinesInputs = "#7F7767",
            TableLines = "#E2DACB",
            Success = "#3F7D4F",
            Warning = "#86590F",
            Error = "#B3402F",
            Info = "#3F6B73",
            SuccessContrastText = "#FFFFFF",
            WarningContrastText = "#FFFFFF",
            ErrorContrastText = "#FFFFFF",
            InfoContrastText = "#FFFFFF",
        }, new PaletteDark());
}
