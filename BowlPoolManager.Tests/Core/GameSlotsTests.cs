using FluentAssertions;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Helpers;

namespace BowlPoolManager.Tests.Core
{
    public class GameSlotsTests
    {
        private static readonly DateTime T = new(2027, 1, 1, 18, 0, 0, DateTimeKind.Utc);

        private static BowlGame Feeder(string id, string bowl, DateTime start, string home, string away, int? hs = null, int? aws = null, GameStatus status = GameStatus.Scheduled, string? next = null) => new()
        {
            Id = id, BowlName = bowl, StartTime = start, TeamHome = home, TeamAway = away,
            TeamHomeScore = hs, TeamAwayScore = aws, Status = status, NextGameId = next, IsPlayoff = true,
            HomeTeamInfo = new TeamInfo { School = home, Abbreviation = home.PadRight(2)[..2].ToUpperInvariant() }
        };

        [Fact]
        public void Resolve_ShouldGiveTheStoredTeam_WhenTheSlotHoldsARealTeam()
        {
            var game = Feeder("g1", "Test Bowl", T, "Iowa", "Utah");

            var home = GameSlots.Resolve(game, isHome: true, new List<BowlGame> { game });

            home.Name.Should().Be("Iowa");
            home.Decided.Should().BeTrue();
            home.Info.Should().NotBeNull();
        }

        [Fact]
        public void Resolve_ShouldNameTheFeedingGame_WhenTheSlotIsEmptyAndTheFeederIsNotFinal()
        {
            var feeder = Feeder("f1", "Cotton Bowl", T, "Miami", "Ohio", next: "g1");
            var game = Feeder("g1", "Sugar Bowl", T.AddDays(1), "", "Georgia");

            var home = GameSlots.Resolve(game, isHome: true, new List<BowlGame> { feeder, game });

            home.Name.Should().Be("Winner of Cotton Bowl");
            home.Decided.Should().BeFalse();
        }

        [Fact]
        public void Resolve_ShouldGiveTheWinner_WhenTheFeederIsFinal()
        {
            var feeder = Feeder("f1", "Cotton Bowl", T, "Miami", "Ohio", 31, 20, GameStatus.Final, next: "g1");
            var game = Feeder("g1", "Sugar Bowl", T.AddDays(1), "", "Georgia");

            var home = GameSlots.Resolve(game, isHome: true, new List<BowlGame> { feeder, game });

            home.Name.Should().Be("Miami");
            home.Decided.Should().BeTrue();
        }

        [Fact]
        public void Resolve_ShouldGiveTbd_WhenNothingFeedsTheEmptySlot()
        {
            var game = Feeder("g1", "Sugar Bowl", T, "", "Georgia");

            var home = GameSlots.Resolve(game, isHome: true, new List<BowlGame> { game });

            home.Name.Should().Be("TBD");
            home.Decided.Should().BeFalse();
        }

        [Fact]
        public void Resolve_ShouldTreatAStoredPlaceholderAsUndecided()
        {
            var game = Feeder("g1", "Sugar Bowl", T, "Winner of Cotton Bowl", "Georgia");

            var home = GameSlots.Resolve(game, isHome: true, new List<BowlGame> { game });

            home.Name.Should().Be("Winner of Cotton Bowl");
            home.Decided.Should().BeFalse();
        }
    }
}
