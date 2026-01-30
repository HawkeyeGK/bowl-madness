using BowlPoolManager.Core.Domain;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BowlPoolManager.Client.Helpers
{
    public static class VisualHelper
    {
        public static bool IsTBD(string name)
        {
            if (string.IsNullOrEmpty(name)) return true;
            if (name.Equals("TBD", StringComparison.OrdinalIgnoreCase)) return true;
            if (name.StartsWith("Winner of", StringComparison.OrdinalIgnoreCase)) return true;
            if (name.StartsWith("Loser of", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public static string GetColor(TeamInfo? info, string resolvedName)
        {
            if (IsTBD(resolvedName)) return "#e9ecef"; // Grey
            return !string.IsNullOrEmpty(info?.Color) ? info.Color : "#ccc";
        }

        public static string GetLogo(TeamInfo? info)
        {
            return !string.IsNullOrEmpty(info?.PrimaryLogoUrl) ? info.PrimaryLogoUrl : "";
        }

        public static TeamInfo? GetTeamInfo(string resolvedName, BowlGame game, List<BowlGame> allGames)
        {
            if (IsTBD(resolvedName)) return null;

            // 1. Try Local Game (Most likely)
            if (string.Equals(resolvedName, game.TeamHome, StringComparison.OrdinalIgnoreCase)) return game.HomeTeamInfo;
            if (string.Equals(resolvedName, game.TeamAway, StringComparison.OrdinalIgnoreCase)) return game.AwayTeamInfo;

            // 2. Scan global games (for advancement scenarios)
            var matchHome = allGames.FirstOrDefault(g => string.Equals(g.TeamHome, resolvedName, StringComparison.OrdinalIgnoreCase));
            if (matchHome != null) return matchHome.HomeTeamInfo;

            var matchAway = allGames.FirstOrDefault(g => string.Equals(g.TeamAway, resolvedName, StringComparison.OrdinalIgnoreCase));
            if (matchAway != null) return matchAway.AwayTeamInfo;

            return null;
        }
    }
}
