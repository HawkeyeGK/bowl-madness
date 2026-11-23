using FluentAssertions;
using Moq;
using BowlPoolManager.Api.Services;
using BowlPoolManager.Api.Services.ScoreFeed;
using BowlPoolManager.Core.Dtos;
using BowlPoolManager.Core.ScoreFeed;

namespace BowlPoolManager.Tests.Api
{
    /// <summary>
    /// The College Football Data feed, its mapping to the shared feed types, and the mock feed's reads.
    /// </summary>
    public class ScoreFeedTests
    {
        [Fact]
        public async Task MockScoreFeed_ShouldReturnStoredGamesWhateverTheYear_WhenAskedForThePostseason()
        {
            var store = new Mock<IMockFeedStore>();
            store.Setup(s => s.GetAllAsync()).ReturnsAsync(new List<MockFeedGame>
            {
                new() { Id = "1", Game = new FeedGame { ExternalId = "1", Status = FeedGameStatus.Final } },
                new() { Id = "2", Game = new FeedGame { ExternalId = "2" } }
            });
            var feed = new MockScoreFeed(store.Object);

            (await feed.GetScoreboardAsync()).Select(g => g.ExternalId).Should().Equal("1", "2");
            (await feed.GetPostseasonGamesAsync(2031)).Should().HaveCount(2);
        }

        // ── CFBD feed and mapping ────────────────────────────────────────────

        private static CfbdGameDto Dto(int id, string? status, bool completed, int? home, int? away) => new()
        {
            Id = id,
            Completed = completed,
            StatusRaw = status,
            Period = 3,
            Clock = "05:12",
            HomeRaw = new Dictionary<string, object> { ["name"] = "Home U", ["id"] = 11, ["points"] = home! },
            AwayRaw = new Dictionary<string, object> { ["name"] = "Away U", ["id"] = 22 },
            AwayPointsRoot = away,
            Notes = "Test Bowl"
        };

        [Theory]
        [InlineData("completed", false, FeedGameStatus.Final)]
        [InlineData(null, true, FeedGameStatus.Final)]
        [InlineData("in_progress", false, FeedGameStatus.InProgress)]
        [InlineData("scheduled", false, FeedGameStatus.Scheduled)]
        [InlineData(null, false, FeedGameStatus.Scheduled)]
        public void ToFeedGame_ShouldMapStatus_WhenCfbdReportsIt(string? raw, bool completed, FeedGameStatus expected)
        {
            CfbdFeedMapping.ToFeedGame(Dto(5, raw, completed, 1, 2)).Status.Should().Be(expected);
        }

        [Fact]
        public void ToFeedGame_ShouldCopyIdsNamesScoresAndClock_WhenCfbdGameIsMapped()
        {
            var game = CfbdFeedMapping.ToFeedGame(Dto(5, "in_progress", false, 14, 7));

            game.ExternalId.Should().Be("5");
            game.Home.Name.Should().Be("Home U");
            game.Home.Id.Should().Be(11);
            game.Home.Points.Should().Be(14);
            game.Away.Name.Should().Be("Away U");
            game.Away.Points.Should().Be(7);
            game.Period.Should().Be(3);
            game.Clock.Should().Be("05:12");
            game.Notes.Should().Be("Test Bowl");
        }

        [Fact]
        public void ToCfbdDto_ShouldRoundTripThroughToFeedGame_WhenGameIsFinal()
        {
            var feed = new FeedGame
            {
                ExternalId = "77", Status = FeedGameStatus.Final, Notes = "Some Bowl",
                Home = new FeedTeam { Name = "A", Id = 1, Points = 21 },
                Away = new FeedTeam { Name = "B", Id = 2, Points = 17 }
            };

            var back = CfbdFeedMapping.ToFeedGame(CfbdFeedMapping.ToCfbdDto(feed));

            back.ExternalId.Should().Be("77");
            back.Status.Should().Be(FeedGameStatus.Final);
            back.Home.Name.Should().Be("A");
            back.Home.Points.Should().Be(21);
            back.Away.Points.Should().Be(17);
            back.Notes.Should().Be("Some Bowl");
        }

        [Fact]
        public void ToCfbdDto_ShouldBeReadableByTheBrowserDto_WhenSerializedAsTheEndpointDoes()
        {
            // The endpoints serialize with System.Text.Json and the admin pages read the result as CfbdGameDto.
            var feed = new FeedGame
            {
                ExternalId = "77", Status = FeedGameStatus.InProgress, Notes = "Some Bowl",
                Home = new FeedTeam { Name = "A", Id = 1, Points = 21 },
                Away = new FeedTeam { Name = "B", Id = 2, Points = 17 }
            };

            var json = System.Text.Json.JsonSerializer.Serialize(new[] { CfbdFeedMapping.ToCfbdDto(feed) });
            var read = System.Text.Json.JsonSerializer.Deserialize<List<CfbdGameDto>>(json)!.Single();

            read.Id.Should().Be(77);
            read.HomeTeam.Should().Be("A");
            read.AwayTeam.Should().Be("B");
            read.HomeId.Should().Be(1);
            read.AwayId.Should().Be(2);
            read.HomePoints.Should().Be(21);
            read.Notes.Should().Be("Some Bowl");
        }

        [Fact]
        public async Task CfbdScoreFeed_ShouldPassTheYearThrough_WhenPostseasonGamesAreRequested()
        {
            var cfbd = new Mock<ICfbdService>();
            cfbd.Setup(c => c.GetPostseasonGamesAsync(It.IsAny<int>()))
                .ReturnsAsync(new List<CfbdGameDto> { Dto(9, null, false, null, null) });
            var feed = new CfbdScoreFeed(cfbd.Object);

            var games = await feed.GetPostseasonGamesAsync(2026);

            games.Should().ContainSingle().Which.ExternalId.Should().Be("9");
            cfbd.Verify(c => c.GetPostseasonGamesAsync(2026), Times.Once);
        }
    }
}
