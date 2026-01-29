using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Core.Helpers
{
    public static class ScoringEngine
    {
        public static List<LeaderboardRow> Calculate(List<BowlGame> games, List<BracketEntry> entries, BowlPool? poolConfig = null)
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
            string? tieBreakerGameId = poolConfig?.TieBreakerGameId;

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

            // SORT ORDER DETERMINATION
            // Default: CorrectPickCount then ScoreDelta
            var primaryMetric = poolConfig?.PrimaryTieBreaker ?? TieBreakerMetric.CorrectPickCount;
            var secondaryMetric = poolConfig?.SecondaryTieBreaker ?? TieBreakerMetric.ScoreDelta;

            // 1. Total Score (Always Descending)
            var sortedQuery = rows.OrderByDescending(r => r.Score);
            IOrderedEnumerable<LeaderboardRow> finalSortedQuery = sortedQuery;

            // 2. Primary Tiebreaker
            if (primaryMetric == TieBreakerMetric.CorrectPickCount)
            {
                finalSortedQuery = sortedQuery.ThenByDescending(r => r.CorrectPicks);
            }
            else // ScoreDelta
            {
                finalSortedQuery = sortedQuery.ThenBy(r => r.TieBreakerDelta ?? int.MaxValue);
            }

            // 3. Secondary Tiebreaker
            if (secondaryMetric == TieBreakerMetric.CorrectPickCount)
            {
                finalSortedQuery = finalSortedQuery.ThenByDescending(r => r.CorrectPicks);
            }
            else // ScoreDelta
            {
                finalSortedQuery = finalSortedQuery.ThenBy(r => r.TieBreakerDelta ?? int.MaxValue);
            }

            // 4. Name (Asc) fallback
            var sortedRows = finalSortedQuery.ThenBy(r => r.Entry.PlayerName).ToList();

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
                    
                    // Determine equality based on configuration
                    bool primaryEqual = IsMetricEqual(primaryMetric, currentRow, prevRow, isTieBreakerFinal);
                    bool secondaryEqual = IsMetricEqual(secondaryMetric, currentRow, prevRow, isTieBreakerFinal);

                    // If everything that matters is equal, give same rank
                    if (scoresEqual && primaryEqual && secondaryEqual)
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

        private static bool IsMetricEqual(TieBreakerMetric metric, LeaderboardRow current, LeaderboardRow prev, bool isTieBreakerFinal)
        {
            if (metric == TieBreakerMetric.CorrectPickCount)
            {
                return current.CorrectPicks == prev.CorrectPicks;
            }
            else // ScoreDelta
            {
                 // If game isn't final, delta is effectively irrelevant/equal for ranking purposes
                 if (!isTieBreakerFinal) return true;

                 // If both have values, compare them
                 if (current.TieBreakerDelta.HasValue && prev.TieBreakerDelta.HasValue)
                 {
                     return current.TieBreakerDelta.Value == prev.TieBreakerDelta.Value;
                 }
                 
                 // If one has value and other doesn't (shouldn't happen if both in same pool context), treat as unequal
                 return current.TieBreakerDelta.HasValue == prev.TieBreakerDelta.HasValue;
            }
        }
    }
}
