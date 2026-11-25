using MudBlazor;

namespace BowlPoolManager.Client.Theme;

/// <summary>
/// The one theme (Release 2). Colors come from the approved prototypes; the same values are CSS custom
/// properties in wwwroot/css/theme.css, so shared components and MudBlazor agree. Fonts ship with the app.
/// </summary>
public static class AppTheme
{
    public const string Navy = "#0F2747";
    public const string Orange = "#C2410C";
    public const string Amber = "#F59E0B";
    public const string PageBackground = "#F4F5F7";

    private static readonly string[] BodyFont = { "Atkinson Hyperlegible", "system-ui", "Segoe UI", "sans-serif" };
    private static readonly string[] HeadingFont = { "Barlow Condensed", "Arial Narrow", "sans-serif" };

    public static MudTheme Theme { get; } = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = Navy,
            Secondary = Orange,
            Tertiary = Amber,
            Background = PageBackground,
            Surface = "#FFFFFF",
            AppbarBackground = Navy,
            AppbarText = "#FFFFFF",
            TextPrimary = "#14171C",
            TextSecondary = "#4A5260",
            LinesDefault = "#E1E4EA",
            Info = "#1D4ED8",
            Success = "#15803D",
            Warning = "#B45309",
            Error = "#B91C1C"
        },
        Typography = new Typography
        {
            Default = new DefaultTypography { FontFamily = BodyFont, FontSize = "1rem", LineHeight = "1.5" },
            H1 = new H1Typography { FontFamily = HeadingFont, FontWeight = "700", FontSize = "2.5rem", LineHeight = "1.1" },
            H2 = new H2Typography { FontFamily = HeadingFont, FontWeight = "700", FontSize = "2rem", LineHeight = "1.15" },
            H3 = new H3Typography { FontFamily = HeadingFont, FontWeight = "700", FontSize = "1.75rem", LineHeight = "1.2" },
            H4 = new H4Typography { FontFamily = HeadingFont, FontWeight = "700", FontSize = "1.5rem", LineHeight = "1.2" },
            H5 = new H5Typography { FontFamily = HeadingFont, FontWeight = "700", FontSize = "1.25rem", LineHeight = "1.25" },
            H6 = new H6Typography { FontFamily = HeadingFont, FontWeight = "600", FontSize = "1.125rem", LineHeight = "1.25" },
            Body1 = new Body1Typography { FontFamily = BodyFont, FontSize = "1rem" },
            Body2 = new Body2Typography { FontFamily = BodyFont, FontSize = "1rem" },
            Button = new ButtonTypography { FontFamily = BodyFont, FontSize = "1.0625rem", FontWeight = "700", TextTransform = "none" },
            Caption = new CaptionTypography { FontFamily = BodyFont, FontSize = "0.9375rem" }
        },
        LayoutProperties = new LayoutProperties { DefaultBorderRadius = "10px" }
    };
}
