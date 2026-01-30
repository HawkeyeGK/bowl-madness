using BowlPoolManager.Core.Domain;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BowlPoolManager.Client.Helpers
{
    public static class BracketHelper
    {
        public static (string Home, string Away) ResolveMatchup(BowlGame game, Dictionary<string, string>? picks, List<BowlGame> allGames)
        {
            string home = game.TeamHome;
            string away = game.TeamAway;

            if (game.IsPlayoff)
            {
                // Look up in all games to find feeders
                // We use allGames to ensure we find feeders even if they aren't in the current view context
                var feeders = allGames.Where(g => g.NextGameId == game.Id).OrderBy(g => g.StartTime).ToList();
                var feederQueue = new Queue<BowlGame>(feeders);

                if (string.IsNullOrWhiteSpace(home) || home.Equals("TBD", StringComparison.OrdinalIgnoreCase))
                {
                    if (feederQueue.TryDequeue(out var f1))
                    {
                        if (picks != null && picks.TryGetValue(f1.Id, out var w1) && !string.IsNullOrEmpty(w1))
                        {
                            home = w1;
                        }
                        else 
                        {
                            home = $"Winner of {f1.BowlName}";
                        }
                    }
                    else 
                    {
                        home = "TBD";
                    }
                }

                if (string.IsNullOrWhiteSpace(away) || away.Equals("TBD", StringComparison.OrdinalIgnoreCase))
                {
                    if (feederQueue.TryDequeue(out var f2))
                    {
                        if (picks != null && picks.TryGetValue(f2.Id, out var w2) && !string.IsNullOrEmpty(w2))
                        {
                            away = w2;
                        }
                        else 
                        {
                            away = $"Winner of {f2.BowlName}";
                        }
                    }
                    else 
                    {
                        away = "TBD";
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(home)) home = "TBD";
            if (string.IsNullOrWhiteSpace(away)) away = "TBD";

            return (home, away);
        }
    }
}
