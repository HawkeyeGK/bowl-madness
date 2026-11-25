using FluentAssertions;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Helpers;

namespace BowlPoolManager.Tests.Core
{
    /// <summary>
    /// The one order for a pool's games, used by fast paper entry and the printed blank entry form. It matches My Picks
    /// and Entry Details: regular bowls by kickoff, then playoff games by kickoff.
    /// </summary>
    public class GameOrderTests
    {
        private static BowlGame Game(string id, int day, int hour = 12, bool playoff = false, TournamentRound round = TournamentRound.Standard) => new()
        {
            Id = id,
            BowlName = id.ToUpperInvariant(),
            IsPlayoff = playoff,
            Round = round,
            StartTime = new DateTime(2026, 12, day, hour, 0, 0, DateTimeKind.Utc)
        };

        private static List<string> Ids(IEnumerable<BowlGame> games) => games.Select(g => g.Id).ToList();

        [Fact]
        public void ForEntry_ShouldOrderRegularBowlsByKickoff_WhenThereIsNoPlayoff()
        {
            var games = new List<BowlGame> { Game("c", 20), Game("a", 15), Game("b", 18) };

            Ids(GameOrder.ForEntry(games)).Should().Equal("a", "b", "c");
        }

        [Fact]
        public void ForEntry_ShouldBreakKickoffTiesById_WhenTwoBowlsStartTogether()
        {
            var games = new List<BowlGame> { Game("b", 15), Game("a", 15) };

            Ids(GameOrder.ForEntry(games)).Should().Equal("a", "b");
        }

        [Fact]
        public void ForEntry_ShouldPutPlayoffGamesAfterRegularBowls_WhenPlayoffKicksOffEarlier()
        {
            var games = new List<BowlGame>
            {
                Game("r1", 10, playoff: true, round: TournamentRound.Round1),
                Game("bowl", 30)
            };

            Ids(GameOrder.ForEntry(games)).Should().Equal("bowl", "r1");
        }

        [Fact]
        public void ForEntry_ShouldOrderPlayoffGamesByKickoff_WhenRoundsDiffer()
        {
            // Kickoff, not round, decides: the second quarterfinal is before the first semifinal and after the first round.
            var games = new List<BowlGame>
            {
                Game("champ", 30, playoff: true, round: TournamentRound.Championship),
                Game("sf", 25, playoff: true, round: TournamentRound.SemiFinal),
                Game("qf2", 20, 18, true, TournamentRound.QuarterFinal),
                Game("qf1", 20, 12, true, TournamentRound.QuarterFinal),
                Game("r1", 15, playoff: true, round: TournamentRound.Round1)
            };

            Ids(GameOrder.ForEntry(games)).Should().Equal("r1", "qf1", "qf2", "sf", "champ");
        }

        [Fact]
        public void ForEntry_ShouldReturnSameOrder_WhenInputOrderChanges()
        {
            var games = new List<BowlGame>
            {
                Game("champ", 30, playoff: true, round: TournamentRound.Championship),
                Game("sf1", 25, 12, true, TournamentRound.SemiFinal),
                Game("sf2", 25, 18, true, TournamentRound.SemiFinal),
                Game("b1", 12), Game("b2", 13), Game("b3", 14)
            };
            var expected = Ids(GameOrder.ForEntry(games));

            var shuffled = games.AsEnumerable().Reverse().ToList();

            Ids(GameOrder.ForEntry(shuffled)).Should().Equal(expected);
            expected.Should().Equal("b1", "b2", "b3", "sf1", "sf2", "champ");
        }

        [Fact]
        public void ForEntry_ShouldReturnEmpty_WhenThereAreNoGames()
        {
            GameOrder.ForEntry(new List<BowlGame>()).Should().BeEmpty();
        }

        [Fact]
        public void ForEntry_ShouldKeepEveryGame_WhenIdsRepeat()
        {
            var games = new List<BowlGame> { Game("a", 10), Game("a", 11) };

            GameOrder.ForEntry(games).Should().HaveCount(2);
        }
    }
}
