using FluentAssertions;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Helpers;

namespace BowlPoolManager.Tests.Core
{
    /// <summary>
    /// Rule checks over many generated seasons (fixed seeds, so a failure repeats). Each season is a
    /// 12-team playoff (four first-round games, four quarterfinals, two semifinals, a final) plus standard
    /// bowls, played in start-time order with results copied forward by BracketPropagation.
    /// </summary>
    public class BracketSimulatorRulesTests
    {
        private const int Seasons = 150;

        private class Season
        {
            public List<BowlGame> Games { get; } = new();
            public List<BracketEntry> Entries { get; } = new();
            public BowlPool Pool { get; } = new();
            public List<string> Teams { get; } = new();
        }

        private static DateTime At(int day, int hour = 12) => new DateTime(2026, 12, 1, hour, 0, 0, DateTimeKind.Utc).AddDays(day);

        private static Season Generate(int seed)
        {
            var rng = new Random(seed);
            var s = new Season();
            BowlGame G(string id, string home, string away, DateTime start, int points, TournamentRound round, string? next = null, bool playoff = false)
            {
                var g = new BowlGame { Id = id, BowlName = id, TeamHome = home, TeamAway = away, StartTime = start, PointValue = points, Round = round, NextGameId = next, IsPlayoff = playoff };
                s.Games.Add(g);
                return g;
            }

            for (int i = 0; i < 5; i++)
            {
                string h = $"S{i}H", a = $"S{i}A";
                s.Teams.Add(h); s.Teams.Add(a);
                G($"std{i}", h, a, At(rng.Next(0, 28), rng.Next(10, 22)), rng.Next(1, 5), TournamentRound.Standard);
            }

            // Seeds 1-4 get byes; 5-12 play the first round (5v12, 6v11, 7v10, 8v9). Quarterfinal home = the bye seed.
            string[] seedName = Enumerable.Range(0, 13).Select(i => $"Seed{i}").ToArray();
            for (int i = 1; i <= 12; i++) s.Teams.Add(seedName[i]);
            string[] firstRound = { "r1a", "r1b", "r1c", "r1d" };
            int[][] pairs = { new[] { 5, 12 }, new[] { 6, 11 }, new[] { 7, 10 }, new[] { 8, 9 } };
            string[] quarters = { "qfa", "qfb", "qfc", "qfd" };
            for (int i = 0; i < 4; i++)
            {
                G(firstRound[i], seedName[pairs[i][0]], seedName[pairs[i][1]], At(20 + i, 12), 1, TournamentRound.Round1, quarters[i], true);
                G(quarters[i], seedName[i + 1], $"Winner of {firstRound[i]}", At(30 + i, 12), 2, TournamentRound.QuarterFinal, i < 2 ? "sfa" : "sfb", true);
            }
            G("sfa", "Winner of qfa", "Winner of qfb", At(40), 4, TournamentRound.SemiFinal, "final", true);
            G("sfb", "Winner of qfc", "Winner of qfd", At(40, 16), 4, TournamentRound.SemiFinal, "final", true);
            G("final", "Winner of sfa", "Winner of sfb", At(50), 8, TournamentRound.Championship, null, true);

            s.Pool.TieBreakerGameId = "final";
            s.Pool.PrimaryTieBreaker = rng.Next(2) == 0 ? TieBreakerMetric.CorrectPickCount : TieBreakerMetric.ScoreDelta;
            s.Pool.SecondaryTieBreaker = rng.Next(2) == 0 ? TieBreakerMetric.CorrectPickCount : TieBreakerMetric.ScoreDelta;

            int entryCount = rng.Next(1, 9);
            for (int e = 0; e < entryCount; e++)
            {
                var entry = new BracketEntry { Id = $"e{e}", PlayerName = $"Player{rng.Next(0, 4)}", TieBreakerPoints = rng.Next(20, 90), Picks = new() };
                foreach (var g in s.Games)
                {
                    if (rng.Next(8) == 0) continue; // an incomplete entry
                    entry.Picks[g.Id] = g.IsPlayoff ? seedName[rng.Next(1, 13)] : (rng.Next(2) == 0 ? g.TeamHome : g.TeamAway);
                }
                s.Entries.Add(entry);
            }

            // Play the season in start-time order, stopping part way. A playoff game is only played when
            // both of its teams are known; results are copied forward like the server does.
            int gamesToPlay = rng.Next(0, s.Games.Count + 1);
            foreach (var g in s.Games.OrderBy(x => x.StartTime).Take(gamesToPlay))
            {
                if (BracketSlots.IsPlaceholder(g.TeamHome) || BracketSlots.IsPlaceholder(g.TeamAway)) continue;
                int home = rng.Next(0, 50), away = rng.Next(0, 50);
                if (home == away) home++;
                g.Status = GameStatus.Final; g.TeamHomeScore = home; g.TeamAwayScore = away;
                BracketPropagation.Propagate(g, s.Games, new List<BowlGame>());
            }
            return s;
        }

        // Chooses a winner for every undecided game, in start-time order, from the teams that can reach it.
        private static Dictionary<string, string> RandomChoices(Season s, int seed, double share)
        {
            var rng = new Random(seed * 31 + 7);
            var chosen = new Dictionary<string, string>();
            foreach (var g in s.Games.OrderBy(x => x.StartTime))
            {
                if (rng.NextDouble() > share) continue;
                var entrants = BracketSimulator.Run(s.Games, s.Entries, s.Pool, chosen).Games[g.Id].Entrants.ToList();
                if (entrants.Count == 0) continue;
                chosen[g.Id] = entrants[rng.Next(entrants.Count)];
            }
            return chosen;
        }

        public static IEnumerable<object[]> SeasonSeeds() => Enumerable.Range(1, Seasons).Select(i => new object[] { i });

        [Theory, MemberData(nameof(SeasonSeeds))]
        public void Run_ShouldNeverScoreAboveMax(int seed)
        {
            var s = Generate(seed);
            foreach (var share in new[] { 0.0, 0.5, 1.0 })
            {
                var result = BracketSimulator.Run(s.Games, s.Entries, s.Pool, RandomChoices(s, seed, share));
                result.Rows.Should().OnlyContain(r => r.Score <= r.MaxPossible);
            }
        }

        [Theory, MemberData(nameof(SeasonSeeds))]
        public void Run_ShouldMatchTheLeaderboardExactly_WhenNothingIsChosen(int seed)
        {
            var s = Generate(seed);

            var sim = BracketSimulator.Run(s.Games, s.Entries, s.Pool);
            var board = ScoringEngine.Calculate(s.Games, s.Entries, s.Pool);

            sim.Rows.Select(r => (r.Entry.Id, r.Rank, r.Score, r.MaxPossible, r.CorrectPicks, r.TieBreakerDelta))
                .Should().Equal(board.Select(r => (r.Entry.Id, r.Rank, r.Score, r.MaxPossible, r.CorrectPicks, r.TieBreakerDelta)));
        }

        [Theory, MemberData(nameof(SeasonSeeds))]
        public void Run_ShouldEqualScoreAndMax_WhenEveryGameIsDecided(int seed)
        {
            var s = Generate(seed);
            var choices = RandomChoices(s, seed, 1.0);

            var result = BracketSimulator.Run(s.Games, s.Entries, s.Pool, choices);

            result.Games.Values.Should().OnlyContain(g => g.Winner != null);
            result.Rows.Should().OnlyContain(r => r.MaxPossible == r.Score);
        }

        [Theory, MemberData(nameof(SeasonSeeds))]
        public void Run_ShouldOnlyAddOpenPicksToMax_WhenAnyChoicesAreMade(int seed)
        {
            var s = Generate(seed);
            var result = BracketSimulator.Run(s.Games, s.Entries, s.Pool, RandomChoices(s, seed, 0.6));
            var points = s.Games.ToDictionary(g => g.Id, g => g.PointValue);

            foreach (var row in result.Rows)
            {
                int open = row.Picks.Where(p => p.Value == PickState.Open).Sum(p => points[p.Key]);
                int won = row.Picks.Where(p => p.Value == PickState.Won).Sum(p => points[p.Key]);
                row.Score.Should().Be(won);
                row.MaxPossible.Should().Be(won + open, "a pick on a team that already lost, or a lost pick, never adds to Max");
            }
        }

        [Theory, MemberData(nameof(SeasonSeeds))]
        public void Run_ShouldChangeNothing_WhenTheRealResultIsChosen(int seed)
        {
            var s = Generate(seed);
            var real = new Dictionary<string, string>();
            foreach (var g in s.Games.Where(x => x.Status == GameStatus.Final)) real[g.Id] = g.WinningTeamName!;

            var plain = BracketSimulator.Run(s.Games, s.Entries, s.Pool);
            var chosen = BracketSimulator.Run(s.Games, s.Entries, s.Pool, real);

            chosen.Rows.Select(r => (r.Entry.Id, r.Rank, r.Score, r.MaxPossible, r.CorrectPicks, r.CanFinishTop3))
                .Should().Equal(plain.Rows.Select(r => (r.Entry.Id, r.Rank, r.Score, r.MaxPossible, r.CorrectPicks, r.CanFinishTop3)));
            chosen.Games.Values.Select(g => (g.Id, g.Winner)).Should().BeEquivalentTo(plain.Games.Values.Select(g => (g.Id, g.Winner)));
        }
    }
}
