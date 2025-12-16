using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Client.Helpers
{
    public class LeaderboardRow
    {
        public int Rank { get; set; }
        public BracketEntry Entry { get; set; } = new();
        public int Score { get; set; }
        public int MaxPossible { get; set; }
        public int CorrectPicks { get; set; }
        public Dictionary<PlayoffRound, int> RoundScores { get; set; } = new();
    }

    public static class ScoringEngine
    {
        public static List<LeaderboardRow> Calculate(List<BowlGame> games, List<BracketEntry> entries)
        {
            // 1. Identify Eliminated Teams (Teams that have lost any FINAL game)
            var eliminatedTeams = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            var finalGames = games.Where(g => g.Status == GameStatus.Final).ToList();
            int totalFinalGames = finalGames.Count;

            foreach (var game in finalGames)
            {
                if (game.TeamHomeScore < game.TeamAwayScore) eliminatedTeams.Add(game.TeamHome);
                else if (game.TeamAwayScore < game.TeamHomeScore) eliminatedTeams.Add(game.TeamAway);
            }

            var rows = new List<LeaderboardRow>();

            foreach (var entry in entries)
            {
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
                            roundScores[game.Round] += game.PointValue;
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

            // UPDATED SORT ORDER: Total -> Max Potential -> Name
            var sortedRows = rows.OrderByDescending(r => r.Score)
                                 .ThenByDescending(r => r.MaxPossible)
                                 .ThenBy(r => r.Entry.PlayerName)
                                 .ToList();

            // Assign Ranks
            // Note: Ranks are still tied based on CURRENT SCORE only.
            // This means visual order might differ from rank number (which is standard).
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
