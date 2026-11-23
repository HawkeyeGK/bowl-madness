using System.Text.Json;
using FluentAssertions;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Helpers;

namespace BowlPoolManager.Tests.Core
{
    /// <summary>
    /// Replays last season's two archived pools (Fixtures/last-season-archive.json: a read-only export, player
    /// names replaced with Player1..N) one game at a time in start-time order, and compares the end with the archive.
    /// </summary>
    public class LastSeasonReplayTests
    {
        private record FxGame(string id, DateTime t, string n, string h, int? hs, string a, int? @as, int pv);
        private record FxStanding(string name, int rank, int pts, int cp, int tbp, int? tbd, Dictionary<string, string> picks);
        private record FxPool(FxPoolConfig pool, List<FxGame> games, List<FxStanding> standings);
        private record FxPoolConfig(string tb, int p1, int p2);

        private static List<FxPool> Load()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "last-season-archive.json");
            return JsonSerializer.Deserialize<List<FxPool>>(File.ReadAllText(path))!;
        }

        public static IEnumerable<object[]> Pools() => Enumerable.Range(0, 2).Select(i => new object[] { i });

        private static (List<BowlGame> games, List<BracketEntry> entries, BowlPool pool) Build(FxPool fx)
        {
            var games = fx.games.OrderBy(g => g.t).Select(g => new BowlGame
            {
                Id = g.id, BowlName = g.n, StartTime = g.t, TeamHome = g.h, TeamAway = g.a, PointValue = g.pv,
                TeamHomeScore = g.hs, TeamAwayScore = g.@as, Status = GameStatus.Final
            }).ToList();
            var entries = fx.standings.Select(s => new BracketEntry { Id = s.name, PlayerName = s.name, TieBreakerPoints = s.tbp, Picks = s.picks }).ToList();
            var pool = new BowlPool { TieBreakerGameId = fx.pool.tb, PrimaryTieBreaker = (TieBreakerMetric)fx.pool.p1, SecondaryTieBreaker = (TieBreakerMetric)fx.pool.p2 };
            return (games, entries, pool);
        }

        [Theory, MemberData(nameof(Pools))]
        public void Replay_ShouldMatchTheArchive_WhenAllGamesAreFinal(int index)
        {
            var fx = Load()[index];
            var (games, entries, pool) = Build(fx);

            var result = BracketSimulator.Run(games, entries, pool);

            foreach (var s in fx.standings)
            {
                var row = result.Rows.Single(r => r.Entry.PlayerName == s.name);
                row.Score.Should().Be(s.pts, $"points for {s.name}");
                row.CorrectPicks.Should().Be(s.cp, $"correct picks for {s.name}");
                row.Rank.Should().Be(s.rank, $"rank for {s.name}");
                row.MaxPossible.Should().Be(row.Score);
            }
        }

        [Theory, MemberData(nameof(Pools))]
        public void Replay_ShouldKeepTheRules_AtEveryStepOfTheSeason(int index)
        {
            var (games, entries, pool) = Build(Load()[index]);
            var previous = new Dictionary<string, (int score, int max)>();

            for (int played = 0; played <= games.Count; played++)
            {
                var stepGames = games.Select((g, i) => new BowlGame
                {
                    Id = g.Id, BowlName = g.BowlName, StartTime = g.StartTime, TeamHome = g.TeamHome, TeamAway = g.TeamAway, PointValue = g.PointValue,
                    Status = i < played ? GameStatus.Final : GameStatus.Scheduled,
                    TeamHomeScore = i < played ? g.TeamHomeScore : null, TeamAwayScore = i < played ? g.TeamAwayScore : null
                }).ToList();

                var sim = BracketSimulator.Run(stepGames, entries, pool);
                var board = ScoringEngine.Calculate(stepGames, entries, pool);

                sim.Rows.Select(r => (r.Entry.Id, r.Rank, r.Score, r.MaxPossible)).Should().Equal(board.Select(r => (r.Entry.Id, r.Rank, r.Score, r.MaxPossible)));
                foreach (var row in sim.Rows)
                {
                    row.Score.Should().BeLessThanOrEqualTo(row.MaxPossible);
                    if (previous.TryGetValue(row.Entry.Id, out var before))
                    {
                        row.Score.Should().BeGreaterThanOrEqualTo(before.score, "points never go down as games finish");
                        row.MaxPossible.Should().BeLessThanOrEqualTo(before.max, "Max never goes up as games finish");
                    }
                    previous[row.Entry.Id] = (row.Score, row.MaxPossible);
                }
            }
        }
    }
}
