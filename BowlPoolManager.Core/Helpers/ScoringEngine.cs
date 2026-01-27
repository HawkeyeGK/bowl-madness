using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Core.Helpers
{
    public static class ScoringEngine
    {
        public static List<LeaderboardRow> Calculate(List<BowlGame> games, List<BracketEntry> entries, string? tieBreakerGameId = null)
        {
            // 1. Identify Eliminated Teams (Teams that have lost any FINAL game)
            var eliminatedTeams = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            var finalGames = games.Where(g => g.Status == GameStatus.Final).ToList();

            foreach (var game in finalGames)
            {
                if (game.TeamHomeScore < game.TeamAwayScore) eliminatedTeams.Add(game.TeamHome);
                else if (game.TeamAwayScore < game.TeamHomeScore) eliminatedTeams.Add(game.TeamAway);
            }

            // 2. Identify Tiebreaker Game Status & Score
            bool isTieBreakerFinal = false;
            int tieBreakerTotalPoints = 0;

            if (!string.IsNullOrEmpty(tieBreakerGameId))
            {
                var tbGame = games.FirstOrDefault(g => g.Id == tieBreakerGameId);
                if (tbGame != null && tbGame.Status == GameStatus.Final)
                {
                    isTieBreakerFinal = true;
                    // Add scores safely
                    tieBreakerTotalPoints = (tbGame.TeamHomeScore ?? 0) + (tbGame.TeamAwayScore ?? 0);
                }
            }

            var rows = new List<LeaderboardRow>();

            foreach (var entry in entries)
            {
                // SAFETY CHECK: Handle Redacted (Hidden) Entries
                if (entry.Picks == null)
                {
                    // FIXED: Initialize the dictionary with ALL keys to prevents UI crashes (KeyNotFound)
                    var safeRoundScores = new Dictionary<PlayoffRound, int> 
                    {
                        { PlayoffRound.Standard, 0 },
                        { PlayoffRound.Round1, 0 },
                        { PlayoffRound.QuarterFinal, 0 },
                        { PlayoffRound.SemiFinal, 0 },
                        { PlayoffRound.Championship, 0 }
                    };

                    rows.Add(new LeaderboardRow 
                    { 
                        Entry = entry, 
                        Score = 0, 
                        MaxPossible = 0, 
                        CorrectPicks = 0,
                        TieBreakerDelta = null,
                        RoundScores = safeRoundScores
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

                // Calculate Tiebreaker Delta if applicable
                int? delta = null;
                if (isTieBreakerFinal)
                {
                    delta = Math.Abs(entry.TieBreakerPoints - tieBreakerTotalPoints);
                }

                rows.Add(new LeaderboardRow 
                { 
                    Entry = entry, 
                    Score = currentScore, 
                    CorrectPicks = correct,
                    RoundScores = roundScores,
                    MaxPossible = maxPossible,
                    TieBreakerDelta = delta
                });
            }

            // SORT ORDER: 
            // 1. Total Score (Desc)
            // 2. Correct Picks (Desc)
            // 3. Tiebreaker Delta (Asc, but ONLY if game is Final) - If not final, we don't sort by it (effectively equal)
            // 4. Name (Asc) fallback

            // To handle null deltas (not final) effectively being "equal", 
            // we can just treat null as Int.MaxValue so they go to bottom if we were sorting, 
            // but for the Rank ASSIGNMENT, we need to be careful.

            // Let's sort optimally for display
            var sortedRows = rows.OrderByDescending(r => r.Score)
                                 .ThenByDescending(r => r.CorrectPicks)
                                 .ThenBy(r => r.TieBreakerDelta ?? int.MaxValue) // Nulls go last or don't matter if all null
                                 .ThenBy(r => r.Entry.PlayerName)
                                 .ToList();

            // Assign Ranks
            int rank = 1;
            for (int i = 0; i < sortedRows.Count; i++)
            {
                var currentRow = sortedRows[i];
                currentRow.Rank = rank; // Default assignment

                if (i > 0)
                {
                    var prevRow = sortedRows[i - 1];

                    bool scoresEqual = currentRow.Score == prevRow.Score;
                    bool picksEqual = currentRow.CorrectPicks == prevRow.CorrectPicks;
                    
                    // Tiebreaker logic: Only breaks ties if the game is FINAL (Delta has value)
                    bool tieBreakerEqual = true;
                    if (isTieBreakerFinal)
                    {
                        // If both have deltas, compare them.
                        if (currentRow.TieBreakerDelta.HasValue && prevRow.TieBreakerDelta.HasValue)
                        {
                            tieBreakerEqual = currentRow.TieBreakerDelta.Value == prevRow.TieBreakerDelta.Value;
                        }
                    }

                    // If everything that matters is equal, give same rank
                    if (scoresEqual && picksEqual && tieBreakerEqual)
                    {
                        currentRow.Rank = prevRow.Rank;
                    }
                    else
                    {
                        currentRow.Rank = rank;
                    }
                }
                
                rank++;
            }

            return sortedRows;
        }
    }
}
