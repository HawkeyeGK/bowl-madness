using System.Text.Json;
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
    // Regression tests for issue 102: the external game calls always asked for season 2025.
    public class GameFunctionsSeasonYearTests
    {
        private class Fixture
        {
            public Mock<IScoreFeed> Feed = new();
            public Mock<ISeasonRepository> Seasons = new();

            public Fixture(params Season[] seasons)
            {
                Seasons.Setup(s => s.GetSeasonsAsync()).ReturnsAsync(seasons.ToList());
                Feed.Setup(f => f.GetPostseasonGamesAsync(It.IsAny<int>())).ReturnsAsync(new List<FeedGame>
                {
                    new() { ExternalId = "42", Home = new FeedTeam { Name = "A" }, Away = new FeedTeam { Name = "B" } }
                });
            }

            public GameFunctions Build() => new(new NullLoggerFactory(), new Mock<IGameRepository>().Object,
                EndpointKit.Users().Object, Feed.Object, Seasons.Object, new Mock<IGameScoringService>().Object);
        }

        private static Season Current(string year) => new() { Id = Guid.NewGuid().ToString(), SeasonId = year, IsCurrent = true };
        private static Season Old(string year) => new() { Id = Guid.NewGuid().ToString(), SeasonId = year, IsCurrent = false };

        [Fact]
        public async Task GetExternalGames_ShouldAskForTheCurrentSeasonYear_WhenCurrentSeasonIsNot2025()
        {
            var f = new Fixture(Old("2025"), Current("2026"));

            var response = await f.Build().GetExternalGames(EndpointKit.Request(Tier.SuperAdmin));

            EndpointKit.Status(response).Should().Be(200);
            f.Feed.Verify(x => x.GetPostseasonGamesAsync(2026), Times.Once);
            f.Feed.Verify(x => x.GetPostseasonGamesAsync(2025), Times.Never);
        }

        [Fact]
        public async Task GetRawCfbdGames_ShouldAskForTheCurrentSeasonYear_WhenCurrentSeasonIsNot2025()
        {
            var f = new Fixture(Old("2025"), Current("2026"));

            var response = await f.Build().GetRawCfbdGames(EndpointKit.Request(Tier.SuperAdmin));

            EndpointKit.Status(response).Should().Be(200);
            f.Feed.Verify(x => x.GetPostseasonGamesAsync(2026), Times.Once);
            f.Feed.Verify(x => x.GetPostseasonGamesAsync(2025), Times.Never);
        }

        [Fact]
        public async Task GetRawCfbdGames_ShouldReturnTheProvidersOwnJson_WhenFeedCanProvideIt()
        {
            var raw = new Mock<IScoreFeed>();
            var rawSource = raw.As<IRawPostseasonJsonSource>();
            rawSource.Setup(r => r.GetRawPostseasonJsonAsync(2026)).ReturnsAsync("[{\"id\":1,\"extra\":\"kept\"}]");
            var f = new Fixture(Current("2026")) { Feed = raw };

            var response = await f.Build().GetRawCfbdGames(EndpointKit.Request(Tier.SuperAdmin));

            EndpointKit.BodyText(response).Should().Be("[{\"id\":1,\"extra\":\"kept\"}]");
        }

        [Fact]
        public async Task GetRawCfbdGames_ShouldReturnGamesInTheCfbdShape_WhenFeedHasNoRawJson()
        {
            var f = new Fixture(Current("2026"));

            var response = await f.Build().GetRawCfbdGames(EndpointKit.Request(Tier.SuperAdmin));

            using var doc = JsonDocument.Parse(EndpointKit.BodyText(response));
            doc.RootElement[0].GetProperty("id").GetInt32().Should().Be(42);
            doc.RootElement[0].GetProperty("homeTeam").GetProperty("name").GetString().Should().Be("A");
        }

        [Fact]
        public async Task GetRawCfbdGames_ShouldServeTheScoreboardFromTheFeed_WhenSourceIsScoreboard()
        {
            var f = new Fixture(Current("2026"));
            f.Feed.Setup(x => x.GetScoreboardAsync(It.IsAny<DateOnly?>())).ReturnsAsync(new List<FeedGame>
            {
                new() { ExternalId = "7", Status = FeedGameStatus.InProgress, Period = 2, Clock = "10:00",
                        Home = new FeedTeam { Name = "H", Id = 3, Points = 10 }, Away = new FeedTeam { Name = "W", Id = 4, Points = 3 } }
            });

            var response = await f.Build().GetRawCfbdGames(EndpointKit.Request(Tier.SuperAdmin, "?source=scoreboard"));

            using var doc = JsonDocument.Parse(EndpointKit.BodyText(response));
            var game = doc.RootElement[0];
            game.GetProperty("id").GetInt32().Should().Be(7);
            game.GetProperty("status").GetString().Should().Be("in_progress");
            game.GetProperty("homeTeam").GetProperty("points").GetInt32().Should().Be(10);
        }

        [Theory]
        [InlineData("GetExternalGames")]
        [InlineData("GetRawCfbdGames")]
        public async Task ExternalGameCalls_ShouldReturn400AndNotCallTheFeed_WhenNoSeasonIsCurrent(string endpoint)
        {
            var f = new Fixture(Old("2025"));
            var functions = f.Build();
            var req = EndpointKit.Request(Tier.SuperAdmin);

            var response = endpoint == "GetExternalGames"
                ? await functions.GetExternalGames(req)
                : await functions.GetRawCfbdGames(req);

            EndpointKit.Status(response).Should().Be(400);
            f.Feed.Verify(x => x.GetPostseasonGamesAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task GetExternalGames_ShouldReturn401AndNotCallTheFeed_WhenAnonymous()
        {
            var f = new Fixture(Current("2026"));

            var response = await f.Build().GetExternalGames(EndpointKit.Request(Tier.Anonymous));

            EndpointKit.Status(response).Should().Be(401);
            f.Feed.Verify(x => x.GetPostseasonGamesAsync(It.IsAny<int>()), Times.Never);
        }
    }
}
