using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Core.Helpers
{
    public static class HoopsWhatIfScoringEngine
    {
        public class HoopsWhatIfRow
        {
            public string EntryId { get; set; } = "";
            public string PlayerName { get; set; } = "";
            public int Rank { get; set; }
            public int Score { get; set; }
            public int MaxPossible { get; set; }
            public int CorrectPicks { get; set; }
            public bool IsEliminated { get; set; }
            public Dictionary<string, string> Picks { get; set; } = new();
        }

        /// <summary>
        /// Calculates projected standings for a basketball What-If simulation.
        /// </summary>
        /// <param name="games">All games for the pool (any round).</param>
        /// <param name="entries">All bracket entries for the pool.</param>
        /// <param name="pool">The pool (used for PointsPerRound hydration).</param>
        /// <param name="simulatedWinners">
        /// Effective winners per game — the caller's pre-computed simulation state
        /// (combines real results and user overrides). Keys = gameId, Values = team name.
        /// </param>
        public static List<HoopsWhatIfRow> Calculate(
            List<HoopsGame> games,
            List<BracketEntry> entries,
            HoopsPool pool,
            Dictionary<string, string?> simulatedWinners)
        {
            // 1. Hydrate point values from the pool's PointsPerRound config.
            var pointValues = new Dictionary<string, int>(games.Count);
            foreach (var game in games)
            {
                int pts = 0;
                if (pool.PointsPerRound != null && pool.PointsPerRound.TryGetValue(game.Round, out var p))
                    pts = p;
                pointValues[game.Id] = pts;
            }

            // 2. Determine effective winners and build the eliminated-team set.
            var effectiveWinners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var eliminatedTeams = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var game in games)
            {
                if (!simulatedWinners.TryGetValue(game.Id, out var winner) || winner == null)
                    continue;

                effectiveWinners[game.Id] = winner;

                // Track the loser of each decided game.
                if (IsRealTeam(game.TeamHome) && !winner.Equals(game.TeamHome, StringComparison.OrdinalIgnoreCase))
                    eliminatedTeams.Add(game.TeamHome);
                if (IsRealTeam(game.TeamAway) && !winner.Equals(game.TeamAway, StringComparison.OrdinalIgnoreCase))
                    eliminatedTeams.Add(game.TeamAway);
            }

            // 3. Score each entry.
            var rows = new List<HoopsWhatIfRow>(entries.Count);

            foreach (var entry in entries)
            {
                if (entry.Picks == null) continue;

                int score = 0, maxPossible = 0, correctPicks = 0;

                foreach (var game in games)
                {
                    int pts = pointValues[game.Id];
                    if (pts == 0) continue;

                    if (!entry.Picks.TryGetValue(game.Id, out var pick) || string.IsNullOrEmpty(pick))
                        continue;

                    if (effectiveWinners.TryGetValue(game.Id, out var winner))
                    {
                        if (pick.Equals(winner, StringComparison.OrdinalIgnoreCase))
                        {
                            score += pts;
                            maxPossible += pts;
                            if (game.Round != TournamentRound.FirstFour)
                                correctPicks++;
                        }
                        // else: wrong pick for a decided game — no points, not added to maxPossible.
                    }
                    else
                    {
                        // Game undecided — the pick is still alive if the team hasn't been eliminated.
                        if (!eliminatedTeams.Contains(pick))
                            maxPossible += pts;
                    }
                }

                rows.Add(new HoopsWhatIfRow
                {
                    EntryId = entry.Id,
                    PlayerName = entry.PlayerName,
                    Score = score,
                    MaxPossible = maxPossible,
                    CorrectPicks = correctPicks,
                    Picks = entry.Picks
                });
            }

            // 4. Sort and assign ranks.
            var sorted = rows
                .OrderByDescending(r => r.Score)
                .ThenByDescending(r => r.MaxPossible)
                .ThenBy(r => r.PlayerName)
                .ToList();

            int leaderScore = sorted.Count > 0 ? sorted[0].Score : 0;
            int rank = 1;

            for (int i = 0; i < sorted.Count; i++)
            {
                if (i > 0 && sorted[i].Score == sorted[i - 1].Score)
                    sorted[i].Rank = sorted[i - 1].Rank;
                else
                    sorted[i].Rank = rank;

                sorted[i].IsEliminated = sorted[i].MaxPossible < leaderScore;
                rank++;
            }

            return sorted;
        }

        /// <summary>Returns true when <paramref name="name"/> is an actual team name (not a TBD placeholder).</summary>
        private static bool IsRealTeam(string? name) =>
            !string.IsNullOrEmpty(name) &&
            !name.Equals("TBD", StringComparison.OrdinalIgnoreCase) &&
            !name.StartsWith("Winner of ", StringComparison.OrdinalIgnoreCase);
    }
}
