using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Core.Helpers
{
    public static class ScoringEngine
    {
        public static List<LeaderboardRow> Calculate(List<BowlGame> games, List<BracketEntry> entries)
        {
            // 1. Identify Eliminated Teams (Teams that have lost any FINAL game)
            var eliminatedTeams = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            var finalGames = games.Where(g => g.Status == GameStatus.Final).ToList();

            foreach (var game in finalGames)
            {
                if (game.TeamHomeScore < game.TeamAwayScore) eliminatedTeams.Add(game.TeamHome);
                else if (game.TeamAwayScore < game.TeamHomeScore) eliminatedTeams.Add(game.TeamAway);
            }

            var rows = new List<LeaderboardRow>();

            foreach (var entry in entries)
            {
                // SAFETY CHECK: Handle Redacted (Hidden) Entries
                if (entry.Picks == null)
                {
                    rows.Add(new LeaderboardRow 
                    { 
                        Entry = entry, 
                        Score = 0, 
                        MaxPossible = 0, 
                        CorrectPicks = 0,
                        RoundScores = new Dictionary<PlayoffRound, int>()
                    });
                    continue;
                }

                int currentScore = 0;
                int correct = 0;
                
                // Initialize Breakdown
                var roundScores = new Dictionary<PlayoffRound, int> 
                {
                    { PlayoffRound.Standard, 0 },
                    { PlayoffRound.Round1, 0 },
                    { PlayoffRound.QuarterFinal, 0 },
                    { PlayoffRound.SemiFinal, 0 },
                    { PlayoffRound.Championship, 0 }
                };

                // Start Max Possible with what they have already banked
                int maxPossible = 0;

                foreach (var game in games)
                {
                    if (!entry.Picks.TryGetValue(game.Id, out var pick)) continue;

                    if (game.Status == GameStatus.Final)
                    {
                        // SCENARIO 1: Game is Over
                        string winner = (game.TeamHomeScore > game.TeamAwayScore) ? game.TeamHome : game.TeamAway;
                        
                        if (string.Equals(pick, winner, StringComparison.OrdinalIgnoreCase))
                        {
                            // Player picked correctly
                            currentScore += game.PointValue;
                            
                            if (roundScores.ContainsKey(game.Round))
                            {
                                roundScores[game.Round] += game.PointValue;
                            }
                            
                            correct++;
                            
                            // Banked points count towards max
                            maxPossible += game.PointValue; 
                        }
                    }
                    else
                    {
                        // SCENARIO 2: Game is Future / In-Progress
                        // If the team I picked has NOT been eliminated yet, I can still win these points.
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
                    CorrectPicks = correct,
                    RoundScores = roundScores,
                    MaxPossible = maxPossible
                });
            }

            // SORT ORDER: Total -> Max Potential -> Name
            var sortedRows = rows.OrderByDescending(r => r.Score)
                                 .ThenByDescending(r => r.MaxPossible)
                                 .ThenBy(r => r.Entry.PlayerName)
                                 .ToList();

            // Assign Ranks
            int rank = 1;
            for (int i = 0; i < sortedRows.Count; i++)
            {
                if (i > 0 && sortedRows[i].Score == sortedRows[i - 1].Score)
                {
                    sortedRows[i].Rank = sortedRows[i - 1].Rank;
                }
                else
                {
                    sortedRows[i].Rank = rank;
                }
                rank++;
            }

            return sortedRows;
        }
    }
}
