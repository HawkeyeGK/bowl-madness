using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Core.Helpers
{
    public enum PickState
    {
        /// <summary>The game is decided and the pick was right.</summary>
        Won,
        /// <summary>The game is decided and the pick was wrong.</summary>
        Lost,
        /// <summary>The game is not decided and the picked team has already lost, so the pick can no longer score.</summary>
        Dead,
        /// <summary>The game is not decided and the picked team is still alive.</summary>
        Open
    }

    /// <summary>One game as the simulation sees it: who plays, and who won (really or by choice).</summary>
    public class SimulatedGame
    {
        public string Id { get; set; } = "";
        /// <summary>The home team, or null when it depends on a game that is not decided.</summary>
        public string? Home { get; set; }
        public string? Away { get; set; }
        /// <summary>Teams that could take the home slot. One name when the slot is known.</summary>
        public List<string> HomeCandidates { get; set; } = new();
        public List<string> AwayCandidates { get; set; } = new();
        /// <summary>Every team that can win this game (home and away candidates together).</summary>
        public IEnumerable<string> Entrants => HomeCandidates.Concat(AwayCandidates);
        public string? Winner { get; set; }
        /// <summary>True when the winner is the game's real result; false when it was chosen.</summary>
        public bool WinnerIsReal { get; set; }
    }

    public class SimulatedRow
    {
        public BracketEntry Entry { get; set; } = new();
        public int Rank { get; set; }
        public int Score { get; set; }
        public int MaxPossible { get; set; }
        public int CorrectPicks { get; set; }
        public int? TieBreakerDelta { get; set; }
        /// <summary>False when the entry's best possible score is below the score of the entry in 3rd place.</summary>
        public bool CanFinishTop3 { get; set; } = true;
        public Dictionary<string, PickState> Picks { get; set; } = new();
    }

    /// <summary>A playoff game that kicks off before a game that feeds it.</summary>
    public class SimulationWarning
    {
        public string GameId { get; set; } = "";
        public string FeederGameId { get; set; } = "";
        public string Message { get; set; } = "";
    }

    public class SimulationResult
    {
        public Dictionary<string, SimulatedGame> Games { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<SimulatedRow> Rows { get; set; } = new();
        public List<SimulationWarning> Warnings { get; } = new();
    }

    /// <summary>
    /// The one football bracket simulator behind Path to Victory. Real final results are used unless the
    /// caller chose a winner for that game. Ranking and Max come from <see cref="ScoringEngine"/>, so with no
    /// chosen winners the rows match the leaderboard exactly. Which slot a feeder fills comes from <see cref="BracketSlots"/>.
    /// </summary>
    public static class BracketSimulator
    {
        /// <param name="games">The pool's games.</param>
        /// <param name="entries">Entries to rank (entries with hidden picks are skipped).</param>
        /// <param name="pool">Tiebreaker settings; may be null.</param>
        /// <param name="chosenWinners">Game id to the team the caller picked to win. A choice that is not a possible entrant of that game is ignored.</param>
        public static SimulationResult Run(
            IReadOnlyList<BowlGame> games,
            IReadOnlyList<BracketEntry> entries,
            BowlPool? pool,
            IReadOnlyDictionary<string, string>? chosenWinners = null)
        {
            var result = new SimulationResult();
            var resolving = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var byId = games.ToDictionary(g => g.Id, StringComparer.OrdinalIgnoreCase);

            foreach (var game in games.OrderBy(g => g.StartTime).ThenBy(g => g.Id, StringComparer.Ordinal))
            {
                Resolve(game);

                foreach (var feeder in BracketSlots.FeedersOf(game, games))
                {
                    if (feeder.StartTime > game.StartTime)
                    {
                        result.Warnings.Add(new SimulationWarning
                        {
                            GameId = game.Id,
                            FeederGameId = feeder.Id,
                            Message = $"{game.BowlName} starts before {feeder.BowlName}, which feeds it."
                        });
                    }
                }
            }

            // Losers of decided games, by name (a team that lost can no longer score).
            var lost = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var sim in result.Games.Values.Where(s => s.Winner != null))
            {
                if (sim.Home != null && !Same(sim.Home, sim.Winner)) lost.Add(sim.Home);
                if (sim.Away != null && !Same(sim.Away, sim.Winner)) lost.Add(sim.Away);
            }

            // Score through ScoringEngine: decided games become Final games.
            var scoringGames = games.Select(g => ToScoringGame(g, result.Games[g.Id])).ToList();
            var tiebreakerGameId = pool?.TieBreakerGameId ?? "";
            var scoringPool = new BowlPool
            {
                PrimaryTieBreaker = pool?.PrimaryTieBreaker ?? TieBreakerMetric.CorrectPickCount,
                SecondaryTieBreaker = pool?.SecondaryTieBreaker ?? TieBreakerMetric.ScoreDelta,
                // A tiebreaker game with a chosen winner has no real total, so the guesses cannot be compared.
                TieBreakerGameId = byId.TryGetValue(tiebreakerGameId, out var tbGame) && UsesRealScores(tbGame, result.Games[tiebreakerGameId])
                    ? tiebreakerGameId
                    : string.Empty
            };

            var visible = entries.Where(e => e.Picks != null).ToList();
            var ranked = ScoringEngine.Calculate(scoringGames, visible, scoringPool);

            // Top 3 pay out: an entry is out when its best score is below the 3rd place score.
            int? thirdPlaceScore = ranked.Count > 3 ? ranked[2].Score : null;

            foreach (var row in ranked)
            {
                var simRow = new SimulatedRow
                {
                    Entry = row.Entry,
                    Rank = row.Rank,
                    Score = row.Score,
                    MaxPossible = row.MaxPossible,
                    CorrectPicks = row.CorrectPicks,
                    TieBreakerDelta = row.TieBreakerDelta,
                    CanFinishTop3 = thirdPlaceScore == null || row.MaxPossible >= thirdPlaceScore
                };

                foreach (var game in games)
                {
                    if (!row.Entry.Picks!.TryGetValue(game.Id, out var pick) || string.IsNullOrEmpty(pick)) continue;
                    var sim = result.Games[game.Id];
                    simRow.Picks[game.Id] = sim.Winner != null
                        ? (Same(pick, sim.Winner) ? PickState.Won : PickState.Lost)
                        : (lost.Contains(pick) ? PickState.Dead : PickState.Open);
                }

                result.Rows.Add(simRow);
            }

            return result;

            // --- local helpers ---

            SimulatedGame Resolve(BowlGame game)
            {
                if (result.Games.TryGetValue(game.Id, out var done)) return done;

                var sim = new SimulatedGame { Id = game.Id };
                // A feeder loop is a data error; treat a game seen twice as having no feeders rather than recursing forever.
                bool inLoop = !resolving.Add(game.Id);

                var feedersBySlot = new Dictionary<bool, List<BowlGame>> { [true] = new(), [false] = new() };
                if (!inLoop)
                {
                    foreach (var feeder in BracketSlots.FeedersOf(game, games))
                        feedersBySlot[BracketSlots.IsHomeSlot(feeder, game, games)].Add(feeder);
                }

                sim.HomeCandidates = Candidates(feedersBySlot[true], game.TeamHome);
                sim.AwayCandidates = Candidates(feedersBySlot[false], game.TeamAway);

                // A slot is known when it has one candidate and every feeder of it is decided (or it has no feeder).
                sim.Home = sim.HomeCandidates.Count == 1 && feedersBySlot[true].All(f => result.Games[f.Id].Winner != null) ? sim.HomeCandidates[0] : null;
                sim.Away = sim.AwayCandidates.Count == 1 && feedersBySlot[false].All(f => result.Games[f.Id].Winner != null) ? sim.AwayCandidates[0] : null;

                var entrants = sim.Entrants.ToList();
                string? chosen = null;
                if (chosenWinners != null && chosenWinners.TryGetValue(game.Id, out var choice))
                    chosen = entrants.FirstOrDefault(t => Same(t, choice));

                string? real = game.Status == GameStatus.Final ? RealWinner(game) : null;
                string? realValid = real == null ? null : entrants.FirstOrDefault(t => Same(t, real));

                if (chosen != null)
                {
                    sim.Winner = chosen;
                    sim.WinnerIsReal = realValid != null && Same(chosen, realValid);
                }
                else if (realValid != null)
                {
                    sim.Winner = realValid;
                    sim.WinnerIsReal = true;
                }

                if (!inLoop) resolving.Remove(game.Id);
                result.Games[game.Id] = sim;
                return sim;
            }

            // A slot with no feeder keeps its stored team; a slot with feeders takes what the feeders produce.
            List<string> Candidates(List<BowlGame> feeders, string stored)
            {
                var names = new List<string>();
                if (feeders.Count == 0)
                {
                    if (!BracketSlots.IsPlaceholder(stored)) names.Add(stored);
                    return names;
                }

                foreach (var feeder in feeders)
                {
                    var feederSim = Resolve(feeder);
                    if (feederSim.Winner != null) AddName(names, feederSim.Winner);
                    else foreach (var t in feederSim.Entrants) AddName(names, t);
                }
                return names;
            }
        }

        private static void AddName(List<string> names, string name)
        {
            if (!names.Any(n => Same(n, name))) names.Add(name);
        }

        private static bool Same(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        // The winner of a Final game from its scores; none when a score is missing or they are level.
        // Same rule as ScoringEngine (issue 133).
        private static string? RealWinner(BowlGame game)
        {
            if (game.TeamHomeScore is not int home || game.TeamAwayScore is not int away || home == away) return null;
            return home > away ? game.TeamHome : game.TeamAway;
        }

        // The real scores can be used only when the teams in the simulation are the teams that really played.
        private static bool UsesRealScores(BowlGame source, SimulatedGame sim) =>
            sim.WinnerIsReal && Same(sim.Home, source.TeamHome) && Same(sim.Away, source.TeamAway);

        private static BowlGame ToScoringGame(BowlGame source, SimulatedGame sim)
        {
            var copy = new BowlGame
            {
                Id = source.Id, BowlName = source.BowlName, PointValue = source.PointValue, Round = source.Round,
                IsPlayoff = source.IsPlayoff, StartTime = source.StartTime,
                TeamHome = source.TeamHome, TeamAway = source.TeamAway, Status = GameStatus.Scheduled
            };

            if (sim.Winner == null) return copy;

            copy.Status = GameStatus.Final;
            if (UsesRealScores(source, sim))
            {
                // The real result, scores included (the tiebreaker needs the real total).
                copy.TeamHomeScore = source.TeamHomeScore;
                copy.TeamAwayScore = source.TeamAwayScore;
                return copy;
            }

            bool homeWon = sim.HomeCandidates.Any(t => Same(t, sim.Winner));
            copy.TeamHome = homeWon ? sim.Winner! : sim.Home ?? "";
            copy.TeamAway = homeWon ? sim.Away ?? "" : sim.Winner!;
            copy.TeamHomeScore = homeWon ? 1 : 0;
            copy.TeamAwayScore = homeWon ? 0 : 1;
            return copy;
        }
    }
}
