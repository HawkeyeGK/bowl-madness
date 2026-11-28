using FluentAssertions;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Helpers;

namespace BowlPoolManager.Tests.Core
{
    // Issue 34: the Rules page shows the selected pool's own lock time, points and tiebreakers.
    public class RulesTextTests
    {
        private static BowlGame Bowl(int points, string id = "") =>
            new BowlGame { Id = id == "" ? Guid.NewGuid().ToString() : id, PointValue = points };

        private static BowlGame Playoff(TournamentRound round, int points) =>
            new BowlGame { IsPlayoff = true, Round = round, PointValue = points };

        [Fact]
        public void PointsByRound_ShouldListEachRoundInOrder_WhenEveryRoundHasOnePointValue()
        {
            var games = new[]
            {
                Playoff(TournamentRound.Championship, 10),
                Bowl(1), Bowl(1),
                Playoff(TournamentRound.SemiFinal, 5), Playoff(TournamentRound.SemiFinal, 5),
                Playoff(TournamentRound.QuarterFinal, 4), Playoff(TournamentRound.QuarterFinal, 4),
                Playoff(TournamentRound.Round1, 3), Playoff(TournamentRound.Round1, 3)
            };

            RulesText.PointsByRound(games).Select(r => (r.Label, r.Points)).Should().Equal(
                ("Bowl games", "1 point each"),
                ("First round", "3 points each"),
                ("Quarterfinals", "4 points each"),
                ("Semifinals", "5 points each"),
                ("Championship", "10 points"));
        }

        [Fact]
        public void PointsByRound_ShouldShowARange_WhenGamesInARoundDiffer()
        {
            var games = new[] { Bowl(2), Bowl(1), Bowl(3) };

            RulesText.PointsByRound(games).Single().Points.Should().Be("1 to 3 points each");
        }

        [Fact]
        public void PointsByRound_ShouldLeaveOutRounds_WhenThePoolHasNoGamesInThem()
        {
            var games = new[] { Bowl(1), Bowl(1), Playoff(TournamentRound.Championship, 1) };

            RulesText.PointsByRound(games).Select(r => (r.Label, r.Points)).Should().Equal(
                ("Bowl games", "1 point each"),
                ("Championship", "1 point"));
        }

        [Fact]
        public void PointsByRound_ShouldLeaveOutEach_WhenARoundHasOneGame()
        {
            RulesText.PointsByRound(new[] { Bowl(2) }).Single().Points.Should().Be("2 points");
        }

        [Fact]
        public void PointsByRound_ShouldCountAPlayoffGameAsABowl_WhenItsRoundWasNeverSet()
        {
            // The admin round list has no Standard option, so a playoff game can keep the default round. It must
            // still be counted (with the bowls, as ScoreGameText labels it) rather than dropped from the list.
            var games = new[] { Bowl(1), new BowlGame { IsPlayoff = true, Round = TournamentRound.Standard, PointValue = 3 } };

            RulesText.PointsByRound(games).Single().Should().Be(new RoundPointsLine("Bowl games", "1 to 3 points each"));
        }

        [Fact]
        public void PointsByRound_ShouldBeEmpty_WhenThereAreNoGames()
        {
            RulesText.PointsByRound(Array.Empty<BowlGame>()).Should().BeEmpty();
        }

        [Fact]
        public void TieBreakers_ShouldNameBothInOrderWithTheGame_WhenThePoolHasATiebreakerGame()
        {
            var pool = new BowlPool
            {
                PrimaryTieBreaker = TieBreakerMetric.CorrectPickCount,
                SecondaryTieBreaker = TieBreakerMetric.ScoreDelta,
                TieBreakerGameId = "g2"
            };
            var games = new[] { new BowlGame { Id = "g1", BowlName = "Rose Bowl" }, new BowlGame { Id = "g2", BowlName = "Citrus Bowl" } };

            RulesText.TieBreakers(pool, games).Should().Equal(
                "The number of correct picks",
                "How close your tiebreaker guess is to the total points scored in the Citrus Bowl");
        }

        [Fact]
        public void TieBreakers_ShouldFollowThePoolsOrder_WhenClosenessComesFirst()
        {
            var pool = new BowlPool
            {
                PrimaryTieBreaker = TieBreakerMetric.ScoreDelta,
                SecondaryTieBreaker = TieBreakerMetric.CorrectPickCount,
                TieBreakerGameId = "g1"
            };
            var games = new[] { new BowlGame { Id = "g1", BowlName = "Rose Bowl" } };

            RulesText.TieBreakers(pool, games).Should().Equal(
                "How close your tiebreaker guess is to the total points scored in the Rose Bowl",
                "The number of correct picks");
        }

        [Fact]
        public void TieBreakers_ShouldListOnce_WhenBothTiebreakersAreTheSame()
        {
            var pool = new BowlPool { PrimaryTieBreaker = TieBreakerMetric.CorrectPickCount, SecondaryTieBreaker = TieBreakerMetric.CorrectPickCount };

            RulesText.TieBreakers(pool, Array.Empty<BowlGame>()).Should().Equal("The number of correct picks");
        }

        [Fact]
        public void TieBreakers_ShouldSayTheTiebreakerGame_WhenThePoolsGameIsNotAmongTheGames()
        {
            var pool = new BowlPool { PrimaryTieBreaker = TieBreakerMetric.ScoreDelta, SecondaryTieBreaker = TieBreakerMetric.ScoreDelta, TieBreakerGameId = "gone" };

            RulesText.TieBreakers(pool, Array.Empty<BowlGame>()).Should().Equal(
                "How close your tiebreaker guess is to the total points scored in the tiebreaker game");
        }

        [Fact]
        public void LockLine_ShouldSayPicksLock_WhenBeforeTheLockTime()
        {
            var lockUtc = new DateTime(2026, 12, 20, 17, 0, 0, DateTimeKind.Utc); // 11:00 AM CST

            RulesText.LockLine(lockUtc, lockUtc.AddDays(-1)).Should().Be("Picks lock Sun, Dec 20 at 11:00 AM CT.");
        }

        [Fact]
        public void LockLine_ShouldStillSayPicksLock_WhenExactlyAtTheLockTime()
        {
            var lockUtc = new DateTime(2026, 12, 20, 17, 0, 0, DateTimeKind.Utc);

            RulesText.LockLine(lockUtc, lockUtc).Should().StartWith("Picks lock ");
        }

        [Fact]
        public void LockLine_ShouldSayPicksLocked_WhenAfterTheLockTime()
        {
            var lockUtc = new DateTime(2026, 12, 20, 17, 0, 0, DateTimeKind.Utc);

            RulesText.LockLine(lockUtc, lockUtc.AddMinutes(1)).Should().Be("Picks locked Sun, Dec 20 at 11:00 AM CT.");
        }
    }
}
