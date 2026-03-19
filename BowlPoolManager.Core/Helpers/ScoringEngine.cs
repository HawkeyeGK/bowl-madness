using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Core.Helpers
{
    public static class ScoringEngine
    {
        // Convenience overload for football callers that pass List<BowlGame>
        public static List<LeaderboardRow> Calculate(List<BowlGame> games, List<BracketEntry> entries, BowlPool? poolConfig = null)
            => Calculate(games.Cast<IScorable>().ToList(), entries, poolConfig);

        public static List<LeaderboardRow> Calculate(List<IScorable> games, List<BracketEntry> entries, BowlPool? poolConfig = null)
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
                    tieBreakerTotalPoints = (tbGame.TeamHomeScore ?? 0) + (tbGame.TeamAwayScore ?? 0);
                }
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
                        TieBreakerDelta = null,
                        RoundScores = BuildRoundScores(games)
                    });
                    continue;
                }

                int currentScore = 0;
                int correct = 0;

                var roundScores = BuildRoundScores(games);

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
                            currentScore += game.PointValue;

                            if (roundScores.ContainsKey(game.Round))
                            {
                                roundScores[game.Round] += game.PointValue;
                            }

                            if (game.Round != TournamentRound.FirstFour)
                                correct++;
                            maxPossible += game.PointValue;
                        }
                    }
                    else
                    {
                        // SCENARIO 2: Game is Future / In-Progress
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
            var primaryMetric = poolConfig?.PrimaryTieBreaker ?? TieBreakerMetric.CorrectPickCount;
            var secondaryMetric = poolConfig?.SecondaryTieBreaker ?? TieBreakerMetric.ScoreDelta;

            var sortedQuery = rows.OrderByDescending(r => r.Score);
            IOrderedEnumerable<LeaderboardRow> finalSortedQuery = sortedQuery;

            if (primaryMetric == TieBreakerMetric.CorrectPickCount)
            {
                finalSortedQuery = sortedQuery.ThenByDescending(r => r.CorrectPicks);
            }
            else
            {
                finalSortedQuery = sortedQuery.ThenBy(r => r.TieBreakerDelta ?? int.MaxValue);
            }

            if (secondaryMetric == TieBreakerMetric.CorrectPickCount)
            {
                finalSortedQuery = finalSortedQuery.ThenByDescending(r => r.CorrectPicks);
            }
            else
            {
                finalSortedQuery = finalSortedQuery.ThenBy(r => r.TieBreakerDelta ?? int.MaxValue);
            }

            var sortedRows = finalSortedQuery.ThenBy(r => r.Entry.PlayerName).ToList();

            // Assign Ranks
            int rank = 1;
            for (int i = 0; i < sortedRows.Count; i++)
            {
                var currentRow = sortedRows[i];
                currentRow.Rank = rank;

                if (i > 0)
                {
                    var prevRow = sortedRows[i - 1];

                    bool scoresEqual = currentRow.Score == prevRow.Score;
                    bool primaryEqual = IsMetricEqual(primaryMetric, currentRow, prevRow, isTieBreakerFinal);
                    bool secondaryEqual = IsMetricEqual(secondaryMetric, currentRow, prevRow, isTieBreakerFinal);

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

        // Builds a RoundScores dictionary seeded with all football rounds plus any additional
        // rounds present in the game list (e.g. basketball rounds). Football keys are always
        // present so the football leaderboard UI can safely access them by key.
        private static Dictionary<TournamentRound, int> BuildRoundScores(IEnumerable<IScorable> games)
        {
            var scores = new Dictionary<TournamentRound, int>
            {
                { TournamentRound.Standard, 0 },
                { TournamentRound.Round1, 0 },
                { TournamentRound.QuarterFinal, 0 },
                { TournamentRound.SemiFinal, 0 },
                { TournamentRound.Championship, 0 }
            };

            foreach (var round in games.Select(g => g.Round).Distinct())
                scores.TryAdd(round, 0);

            return scores;
        }

        private static bool IsMetricEqual(TieBreakerMetric metric, LeaderboardRow current, LeaderboardRow prev, bool isTieBreakerFinal)
        {
            if (metric == TieBreakerMetric.CorrectPickCount)
            {
                return current.CorrectPicks == prev.CorrectPicks;
            }
            else
            {
                if (!isTieBreakerFinal) return true;

                if (current.TieBreakerDelta.HasValue && prev.TieBreakerDelta.HasValue)
                {
                    return current.TieBreakerDelta.Value == prev.TieBreakerDelta.Value;
                }

                return current.TieBreakerDelta.HasValue == prev.TieBreakerDelta.HasValue;
            }
        }
    }
}
