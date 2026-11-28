using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using BowlPoolManager.Api.Functions;
using BowlPoolManager.Api.Repositories;
using BowlPoolManager.Api.Services;
using BowlPoolManager.Api.Services.ScoreFeed;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Dtos;

namespace BowlPoolManager.Tests.Api
{
    /// <summary>
    /// Issue 138: football SaveGame refuses bad input the way SaveHoopsGame does, and SaveGameResult (the Game Results
    /// page) changes only the status and scores of the stored game, so stale fields from the page are never written.
    /// </summary>
    public class SaveGameInputAndResultTests
    {
        private class Fixture
        {
            public Mock<IGameRepository> Games = new();
            public Mock<IGameScoringService> Scoring = new();
            public BowlGame Stored = new()
            {
                Id = "g1", SeasonId = "season-1", BowlName = "Rose Bowl", TeamHome = "Utah", TeamAway = "Ohio State",
                ExternalId = "401", ApiHomeTeam = "Utah", ApiAwayTeam = "Ohio State", HomeApRank = 12, NextGameId = "g9"
            };

            public Fixture()
            {
                Games.Setup(g => g.GetGamesAsync(It.IsAny<string?>())).ReturnsAsync(() => new List<BowlGame> { Stored });
            }

            public GameFunctions Build() => new(new NullLoggerFactory(), Games.Object, EndpointKit.Users().Object,
                new Mock<IScoreFeed>().Object, new Mock<ISeasonRepository>().Object, Scoring.Object);
        }

        // ── SaveGame input handling ──────────────────────────────────────────────

        [Fact]
        public async Task SaveGame_ShouldReturn400AndSaveNothing_WhenTheBodyIsNotJson()
        {
            var f = new Fixture();

            var result = await f.Build().SaveGame(EndpointKit.RequestWithRawBody(Tier.SuperAdmin, "{not json"));

            EndpointKit.Status(result).Should().Be(400);
            f.Scoring.Verify(s => s.ProcessGameUpdateAsync(It.IsAny<BowlGame>()), Times.Never);
        }

        [Fact]
        public async Task SaveGame_ShouldReturn400AndSaveNothing_WhenTheBodyIsNull()
        {
            var f = new Fixture();

            var result = await f.Build().SaveGame(EndpointKit.RequestWithRawBody(Tier.SuperAdmin, "null"));

            EndpointKit.Status(result).Should().Be(400);
            f.Scoring.Verify(s => s.ProcessGameUpdateAsync(It.IsAny<BowlGame>()), Times.Never);
        }

        [Theory]
        [InlineData("", "season-1")]
        [InlineData("g1", "")]
        public async Task SaveGame_ShouldReturn400AndSaveNothing_WhenTheIdOrSeasonIsMissing(string id, string seasonId)
        {
            var f = new Fixture();
            var game = new BowlGame { Id = id, SeasonId = seasonId, BowlName = "Rose Bowl" };

            var result = await f.Build().SaveGame(EndpointKit.Request(Tier.SuperAdmin, body: game));

            EndpointKit.Status(result).Should().Be(400);
            EndpointKit.BodyText(result).Should().Contain("id and a season");
            f.Scoring.Verify(s => s.ProcessGameUpdateAsync(It.IsAny<BowlGame>()), Times.Never);
        }

        [Fact]
        public async Task SaveGame_ShouldReturn500_WhenSavingThrows()
        {
            var f = new Fixture();
            f.Scoring.Setup(s => s.ProcessGameUpdateAsync(It.IsAny<BowlGame>())).ThrowsAsync(new InvalidOperationException("boom"));

            var result = await f.Build().SaveGame(EndpointKit.Request(Tier.SuperAdmin, body: f.Stored));

            EndpointKit.Status(result).Should().Be(500);
        }

        [Fact]
        public async Task SaveGame_ShouldRefuseBeforeReadingTheBody_WhenCallerIsAdmin()
        {
            var f = new Fixture();

            var result = await f.Build().SaveGame(EndpointKit.RequestWithRawBody(Tier.Admin, "{not json"));

            EndpointKit.Status(result).Should().Be(403);
        }

        // ── SaveGameResult ───────────────────────────────────────────────────────

        private static GameResultUpdate Result(GameStatus status, int? home, int? away, string gameId = "g1", string seasonId = "season-1") => new()
        {
            GameId = gameId, SeasonId = seasonId, Status = status, TeamHomeScore = home, TeamAwayScore = away
        };

        [Theory]
        [InlineData(Tier.Anonymous, 401)]
        [InlineData(Tier.Player, 403)]
        [InlineData(Tier.Admin, 403)]
        public async Task SaveGameResult_ShouldRefuseAndSaveNothing_WhenCallerIsNotSuperAdmin(Tier tier, int expected)
        {
            var f = new Fixture();

            var result = await f.Build().SaveGameResult(EndpointKit.Request(tier, body: Result(GameStatus.Final, 21, 14)));

            EndpointKit.Status(result).Should().Be(expected);
            f.Games.Verify(g => g.GetGamesAsync(It.IsAny<string?>()), Times.Never);
            f.Scoring.Verify(s => s.ProcessGameUpdateAsync(It.IsAny<BowlGame>()), Times.Never);
        }

        [Fact]
        public async Task SaveGameResult_ShouldChangeOnlyStatusAndScores_WhenSuperAdmin()
        {
            var f = new Fixture();
            BowlGame? saved = null;
            f.Scoring.Setup(s => s.ProcessGameUpdateAsync(It.IsAny<BowlGame>())).Callback<BowlGame>(g => saved = g).Returns(Task.CompletedTask);

            var result = await f.Build().SaveGameResult(EndpointKit.Request(Tier.SuperAdmin, body: Result(GameStatus.Final, 21, 14)));

            EndpointKit.Status(result).Should().Be(200);
            saved.Should().NotBeNull();
            saved!.Status.Should().Be(GameStatus.Final);
            saved.TeamHomeScore.Should().Be(21);
            saved.TeamAwayScore.Should().Be(14);
            // Everything else comes from the stored game, not the page.
            saved.TeamHome.Should().Be("Utah");
            saved.TeamAway.Should().Be("Ohio State");
            saved.ExternalId.Should().Be("401");
            saved.HomeApRank.Should().Be(12);
            saved.NextGameId.Should().Be("g9");
            f.Games.Verify(g => g.GetGamesAsync("season-1"), Times.Once);
        }

        [Theory]
        [InlineData(null, 14)]
        [InlineData(21, null)]
        [InlineData(17, 17)]
        public async Task SaveGameResult_ShouldRefuseAndSaveNothing_WhenAFinalHasNoWinner(int? home, int? away)
        {
            var f = new Fixture();

            var result = await f.Build().SaveGameResult(EndpointKit.Request(Tier.SuperAdmin, body: Result(GameStatus.Final, home, away)));

            EndpointKit.Status(result).Should().Be(400);
            EndpointKit.BodyText(result).Should().Contain("cannot end tied");
            f.Scoring.Verify(s => s.ProcessGameUpdateAsync(It.IsAny<BowlGame>()), Times.Never);
        }

        [Theory]
        [InlineData("", "season-1")]
        [InlineData("g1", "")]
        public async Task SaveGameResult_ShouldRefuseAndSaveNothing_WhenTheGameOrSeasonIsMissing(string gameId, string seasonId)
        {
            var f = new Fixture();

            var result = await f.Build().SaveGameResult(EndpointKit.Request(Tier.SuperAdmin, body: Result(GameStatus.InProgress, 7, 0, gameId, seasonId)));

            EndpointKit.Status(result).Should().Be(400);
            f.Scoring.Verify(s => s.ProcessGameUpdateAsync(It.IsAny<BowlGame>()), Times.Never);
        }

        [Fact]
        public async Task SaveGameResult_ShouldReturn400_WhenTheBodyIsNotJson()
        {
            var f = new Fixture();

            var result = await f.Build().SaveGameResult(EndpointKit.RequestWithRawBody(Tier.SuperAdmin, "[1,2"));

            EndpointKit.Status(result).Should().Be(400);
            f.Scoring.Verify(s => s.ProcessGameUpdateAsync(It.IsAny<BowlGame>()), Times.Never);
        }

        [Fact]
        public async Task SaveGameResult_ShouldReturn400_WhenTheStatusIsNotAKnownValue()
        {
            var f = new Fixture();

            var result = await f.Build().SaveGameResult(EndpointKit.RequestWithRawBody(Tier.SuperAdmin,
                "{\"gameId\":\"g1\",\"seasonId\":\"season-1\",\"gameStatus\":42}"));

            EndpointKit.Status(result).Should().Be(400);
            f.Scoring.Verify(s => s.ProcessGameUpdateAsync(It.IsAny<BowlGame>()), Times.Never);
        }

        [Fact]
        public async Task SaveGameResult_ShouldReturn404AndSaveNothing_WhenTheGameIsNotInThatSeason()
        {
            var f = new Fixture();

            var result = await f.Build().SaveGameResult(EndpointKit.Request(Tier.SuperAdmin, body: Result(GameStatus.InProgress, 7, 0, gameId: "other")));

            EndpointKit.Status(result).Should().Be(404);
            f.Scoring.Verify(s => s.ProcessGameUpdateAsync(It.IsAny<BowlGame>()), Times.Never);
        }
    }
}
