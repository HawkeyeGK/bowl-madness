using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using BowlPoolManager.Api.Functions;
using BowlPoolManager.Api.Repositories;
using BowlPoolManager.Api.Services;
using BowlPoolManager.Api.Services.ScoreFeed;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.ScoreFeed;

namespace BowlPoolManager.Tests.Api
{
    /// <summary>AP rank is typed by a SuperAdmin on the games page; the server refuses a rank outside 1 to 25.</summary>
    public class SaveGameApRankTests
    {
        private static (GameFunctions sut, Mock<IGameScoringService> scoring) Build()
        {
            var scoring = new Mock<IGameScoringService>();
            var sut = new GameFunctions(new NullLoggerFactory(), new Mock<IGameRepository>().Object,
                EndpointKit.Users().Object, new Mock<IScoreFeed>().Object, new Mock<ISeasonRepository>().Object, scoring.Object);
            return (sut, scoring);
        }

        private static BowlGame Game(int? home, int? away) => new()
        {
            Id = "g1", SeasonId = "season-1", BowlName = "Test Bowl", TeamHome = "Home", TeamAway = "Away",
            HomeApRank = home, AwayApRank = away
        };

        [Theory]
        [InlineData(0, null)]
        [InlineData(null, 26)]
        [InlineData(-1, 5)]
        [InlineData(30, 30)]
        public async Task SaveGame_ShouldRefuseAndSaveNothing_WhenAnApRankIsOutsideTheTop25(int? home, int? away)
        {
            var (sut, scoring) = Build();

            var result = await sut.SaveGame(EndpointKit.Request(Tier.SuperAdmin, body: Game(home, away)));

            EndpointKit.Status(result).Should().Be(400);
            EndpointKit.BodyText(result).Should().Contain("AP rank");
            scoring.Verify(s => s.ProcessGameUpdateAsync(It.IsAny<BowlGame>()), Times.Never);
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData(1, 25)]
        [InlineData(15, null)]
        public async Task SaveGame_ShouldSaveTheRanks_WhenTheyAreMissingOrInTheTop25(int? home, int? away)
        {
            var (sut, scoring) = Build();

            var result = await sut.SaveGame(EndpointKit.Request(Tier.SuperAdmin, body: Game(home, away)));

            EndpointKit.Status(result).Should().Be(200);
            scoring.Verify(s => s.ProcessGameUpdateAsync(It.Is<BowlGame>(g => g.HomeApRank == home && g.AwayApRank == away)), Times.Once);
        }

        [Fact]
        public async Task SaveGame_ShouldRefuseAnApRankBeforeAnythingIsSaved_WhenCallerIsPlayer()
        {
            var (sut, scoring) = Build();

            var result = await sut.SaveGame(EndpointKit.Request(Tier.Player, body: Game(15, null)));

            EndpointKit.Status(result).Should().Be(403);
            scoring.Verify(s => s.ProcessGameUpdateAsync(It.IsAny<BowlGame>()), Times.Never);
        }
    }
}
