using MudBlazor;

namespace BowlPoolManager.Client.Ui;

/// <summary>The icon for each navigation key from <c>SiteNavigation</c> (Core).</summary>
public static class NavIcons
{
    public static string For(string key) => key switch
    {
        "leaderboard" => Icons.Material.Outlined.EmojiEvents,
        "scores" => Icons.Material.Outlined.Scoreboard,
        "whatif" => Icons.Material.Outlined.AltRoute,
        "picks" => Icons.Material.Outlined.Checklist,
        "history" => Icons.Material.Outlined.History,
        "print" => Icons.Material.Outlined.Print,
        "rules" => Icons.Material.Outlined.MenuBook,
        "admin" => Icons.Material.Outlined.AdminPanelSettings,
        _ => Icons.Material.Outlined.Circle
    };
}
