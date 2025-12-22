using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Core.Helpers
{
    public static class WhatIfScoringEngine
    {
        public static List<LeaderboardRow> Calculate(
            List<BowlGame> games, 
            List<BracketEntry> entries, 
            Dictionary<string, string> simulatedWinners)
        {
            // 1. Determine Effective Winners (Real vs Simulated)
            // We pre-calculate this look-up for performance
            var effectiveWinners = new Dictionary<string, string>();
            var eliminatedTeams = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var game in games)
            {
                string? winner = null;

                // Priority 1: Simulation
                if (simulatedWinners.TryGetValue(game.Id, out var simWinner))
                {
                    winner = simWinner;
                }
                // Priority 2: Real Life Final
                else if (game.Status == GameStatus.Final)
                {
                    int h = game.TeamHomeScore ?? 0;
                    int a = game.TeamAwayScore ?? 0;
                    if (h > a) winner = game.TeamHome;
                    else if (a > h) winner = game.TeamAway;
                }

                if (winner != null)
                {
                    effectiveWinners[game.Id] = winner;
                    
                    // Identify Loser for Elimination Logic (Simple version for Standard games)
                    // For Playoff/TBD games, we can't easily deduce the loser without tree traversal,
                    // but for the matrix math, knowing the WINNER is usually sufficient.
                    if (!string.Equals(winner, game.TeamHome, StringComparison.OrdinalIgnoreCase) && !string.Equals(game.TeamHome, "TBD", StringComparison.OrdinalIgnoreCase))
                         eliminatedTeams.Add(game.TeamHome);
                    if (!string.Equals(winner, game.TeamAway, StringComparison.OrdinalIgnoreCase) && !string.Equals(game.TeamAway, "TBD", StringComparison.OrdinalIgnoreCase))
                         eliminatedTeams.Add(game.TeamAway);
                }
            }

            var rows = new List<LeaderboardRow>();

            foreach (var entry in entries)
            {
                if (entry.Picks == null) continue;

                int currentScore = 0;
                int maxPossible = 0;
                
                // For the simplified Matrix view, we just count raw correct picks
                // The main Leaderboard logic is more complex with RoundScores, but this suffices for projections.
                int correct = 0; 

                foreach (var game in games)
                {
                    if (!entry.Picks.TryGetValue(game.Id, out var pick)) continue;

                    bool gameIsDecided = effectiveWinners.ContainsKey(game.Id);
                    
                    if (gameIsDecided)
                    {
                        string winner = effectiveWinners[game.Id];
                        if (string.Equals(pick, winner, StringComparison.OrdinalIgnoreCase))
                        {
                            currentScore += game.PointValue;
                            maxPossible += game.PointValue;
                            correct++;
                        }
                    }
                    else
                    {
                        // Game is UNDECIDED (Real future + No Sim)
                        // Tie-breaking logic: If my team is eliminated, I can't win points.
                        if (!eliminatedTeams.Contains(pick))
                        {
                            maxPossible += game.PointValue;
                        }
                    }
                }

                rows.Add(new LeaderboardRow 
                { 
                    Entry = entry, 
                    Score = currentScore, 
                    MaxPossible = maxPossible,
                    CorrectPicks = correct
                });
            }

            // Standard Sort
            var sorted = rows.OrderByDescending(r => r.Score)
                       .ThenByDescending(r => r.MaxPossible)
                       .ThenBy(r => r.Entry.PlayerName)
                       .ToList();

            // Assign Ranks
            int rank = 1;
            for (int i = 0; i < sorted.Count; i++)
            {
                if (i > 0 && sorted[i].Score == sorted[i - 1].Score)
                    sorted[i].Rank = sorted[i - 1].Rank;
                else
                    sorted[i].Rank = rank;
                rank++;
            }

            return sorted;
        }
    }
}
