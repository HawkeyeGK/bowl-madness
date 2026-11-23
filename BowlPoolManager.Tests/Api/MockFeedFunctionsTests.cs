using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using BowlPoolManager.Api.Functions.Admin;
using BowlPoolManager.Api.Services.ScoreFeed;
using BowlPoolManager.Core.ScoreFeed;

namespace BowlPoolManager.Tests.Api
{
    /// <summary>
    /// The mock feed tools write feed data that drives scoring, on any site. Order of checks: caller (SuperAdmin)
    /// on the server first, then the input, and only then the store, the mode or the games.
    /// </summary>
    public class MockFeedFunctionsTests
    {
        private class Fixture
        {
            public Mock<IMockFeedStore> Store = new();
            public Mock<IMockFeedStartingSet> StartingSet = new();
            public Mock<IScoreFeedSettingsStore> Settings = new();
            public Mock<IMockFeedResetService> Reset = new();

            public Fixture()
            {
                Store.Setup(s => s.GetAllAsync()).ReturnsAsync(new List<MockFeedGame>
                {
                    new() { Id = "2", Game = new FeedGame { ExternalId = "2" } },
                    new() { Id = "1", Game = new FeedGame { ExternalId = "1" } }
                });
                Store.Setup(s => s.UpsertAsync(It.IsAny<MockFeedGame>())).Returns(Task.CompletedTask);
                Store.Setup(s => s.DeleteAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
                Store.Setup(s => s.ReplaceAllAsync(It.IsAny<IEnumerable<FeedGame>>())).ReturnsAsync(3);
                StartingSet.Setup(s => s.Load()).Returns(new List<FeedGame> { new() { ExternalId = "9" } });
                Settings.Setup(s => s.GetModeAsync()).ReturnsAsync(ScoreFeedMode.Mock);
                Settings.Setup(s => s.SetModeAsync(It.IsAny<ScoreFeedMode>())).Returns(Task.CompletedTask);
                var summary = new MockFeedResetSummary { GamesToReset = 4, SlotsToClear = 2 };
                Reset.Setup(r => r.PreviewAsync()).ReturnsAsync(new MockFeedResetResult(MockFeedResetStatus.Ok, summary));
                Reset.Setup(r => r.ApplyAsync(4)).ReturnsAsync(new MockFeedResetResult(MockFeedResetStatus.Ok, summary));
                Reset.Setup(r => r.ApplyAsync(It.Is<int>(n => n != 4))).ReturnsAsync(new MockFeedResetResult(MockFeedResetStatus.CountChanged, summary));
            }

            public MockFeedFunctions Build() => new(new NullLoggerFactory(), EndpointKit.Users().Object,
                Store.Object, StartingSet.Object, Settings.Object, Reset.Object);

            public void NothingTouched()
            {
                Store.Verify(s => s.GetAllAsync(), Times.Never);
                Store.Verify(s => s.UpsertAsync(It.IsAny<MockFeedGame>()), Times.Never);
                Store.Verify(s => s.DeleteAsync(It.IsAny<string>()), Times.Never);
                Store.Verify(s => s.ReplaceAllAsync(It.IsAny<IEnumerable<FeedGame>>()), Times.Never);
                StartingSet.Verify(s => s.Load(), Times.Never);
                Settings.Verify(s => s.GetModeAsync(), Times.Never);
                Settings.Verify(s => s.SetModeAsync(It.IsAny<ScoreFeedMode>()), Times.Never);
                Reset.Verify(r => r.PreviewAsync(), Times.Never);
                Reset.Verify(r => r.ApplyAsync(It.IsAny<int>()), Times.Never);
            }
        }

        private static FeedGame ValidGame() => new()
        {
            ExternalId = "5", Status = FeedGameStatus.InProgress, Period = 2, Clock = "08:15",
            Home = new FeedTeam { Name = "A", Points = 7 }, Away = new FeedTeam { Name = "B", Points = 3 }
        };

        private record Result(int Status, string Body);

        private static async Task<Result> Call(MockFeedFunctions f, string endpoint, Tier tier, object? body = null, string query = "")
        {
            var req = EndpointKit.Request(tier, query, body);
            var response = endpoint switch
            {
                "get" => await f.GetMockFeedGames(req),
                "save" => await f.SaveMockFeedGame(req),
                "delete" => await f.DeleteMockFeedGame(req, "5"),
                "startingSet" => await f.LoadMockFeedStartingSet(req),
                "getMode" => await f.GetMockFeedMode(req),
                "setMode" => await f.SetMockFeedMode(req),
                "preview" => await f.PreviewMockFeedReset(req),
                _ => throw new ArgumentException(endpoint)
            };
            return new Result(EndpointKit.Status(response), EndpointKit.BodyText(response));
        }

        public static IEnumerable<object[]> AllEndpoints() =>
            new[] { "get", "save", "delete", "startingSet", "getMode", "setMode", "preview" }.Select(e => new object[] { e });

        [Theory]
        [MemberData(nameof(AllEndpoints))]
        public async Task Endpoints_ShouldReturn401AndTouchNothing_WhenAnonymous(string endpoint)
        {
            var f = new Fixture();

            var result = await Call(f.Build(), endpoint, Tier.Anonymous, new { mode = "Mock" }, "?confirm=4");

            result.Status.Should().Be(401);
            f.NothingTouched();
        }

        [Theory]
        [MemberData(nameof(AllEndpoints))]
        public async Task Endpoints_ShouldReturn403AndTouchNothing_WhenCallerIsAPlayer(string endpoint)
        {
            var f = new Fixture();

            var result = await Call(f.Build(), endpoint, Tier.Player, new { mode = "Mock" }, "?confirm=4");

            result.Status.Should().Be(403);
            f.NothingTouched();
        }

        [Theory]
        [MemberData(nameof(AllEndpoints))]
        public async Task Endpoints_ShouldReturn403AndTouchNothing_WhenCallerIsAnAdminButNotSuperAdmin(string endpoint)
        {
            var f = new Fixture();

            var result = await Call(f.Build(), endpoint, Tier.Admin, new { mode = "Mock" }, "?confirm=4");

            result.Status.Should().Be(403);
            f.NothingTouched();
        }

        // ── Games ────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetMockFeedGames_ShouldReturnGamesInIdOrder_WhenSuperAdmin()
        {
            var result = await Call(new Fixture().Build(), "get", Tier.SuperAdmin);

            result.Status.Should().Be(200);
            result.Body.IndexOf("\"externalId\":\"1\"").Should().BeLessThan(result.Body.IndexOf("\"externalId\":\"2\""));
        }

        [Fact]
        public async Task SaveMockFeedGame_ShouldStoreTheGame_WhenInputIsValid()
        {
            var f = new Fixture();

            var result = await Call(f.Build(), "save", Tier.SuperAdmin, ValidGame());

            result.Status.Should().Be(200);
            f.Store.Verify(s => s.UpsertAsync(It.Is<MockFeedGame>(m =>
                m.Game.ExternalId == "5" && m.Game.Status == FeedGameStatus.InProgress && m.Game.Home.Points == 7)), Times.Once);
        }

        [Fact]
        public async Task SaveMockFeedGame_ShouldAcceptAFinalWithNoScores_WhenSavedForTestingMismatches()
        {
            // The page exists to build bad feed data (issue 133), so this is allowed on purpose.
            var game = ValidGame();
            game.Status = FeedGameStatus.Final;
            game.Home.Points = null;
            game.Away.Points = null;

            var result = await Call(new Fixture().Build(), "save", Tier.SuperAdmin, game);

            result.Status.Should().Be(200);
        }

        [Fact]
        public async Task SaveMockFeedGame_ShouldReturn400AndNotStore_WhenBodyIsMissing()
        {
            var f = new Fixture();

            var result = await Call(f.Build(), "save", Tier.SuperAdmin);

            result.Status.Should().Be(400);
            f.Store.Verify(s => s.UpsertAsync(It.IsAny<MockFeedGame>()), Times.Never);
        }

        [Fact]
        public async Task SaveMockFeedGame_ShouldReturn400AndNotStore_WhenHomeOrAwayIsNull()
        {
            var f = new Fixture();

            var result = await Call(f.Build(), "save", Tier.SuperAdmin, new { externalId = "5", home = (object?)null, away = (object?)null });

            result.Status.Should().Be(400);
            f.Store.Verify(s => s.UpsertAsync(It.IsAny<MockFeedGame>()), Times.Never);
        }

        [Fact]
        public void Validate_ShouldRefuse_WhenIdIsMissingOrScoreIsOutOfRange()
        {
            var noId = ValidGame(); noId.ExternalId = " ";
            var negative = ValidGame(); negative.Home.Points = -1;
            var huge = ValidGame(); huge.Away.Points = 5000;
            var longName = ValidGame(); longName.Home.Name = new string('x', 101);

            MockFeedFunctions.Validate(noId).Should().NotBeNull();
            MockFeedFunctions.Validate(negative).Should().NotBeNull();
            MockFeedFunctions.Validate(huge).Should().NotBeNull();
            MockFeedFunctions.Validate(longName).Should().NotBeNull();
            MockFeedFunctions.Validate(ValidGame()).Should().BeNull();
        }

        [Theory]
        [InlineData("a/b")]
        [InlineData("a\\b")]
        [InlineData("a?b")]
        [InlineData("a#b")]
        [InlineData("a b")]
        public void Validate_ShouldRefuse_WhenIdHasCharactersCosmosForbids(string id)
        {
            var game = ValidGame(); game.ExternalId = id;

            MockFeedFunctions.Validate(game).Should().NotBeNull();
        }

        [Fact]
        public async Task LoadMockFeedStartingSet_ShouldReplaceAllGames_WhenSuperAdmin()
        {
            var f = new Fixture();

            var result = await Call(f.Build(), "startingSet", Tier.SuperAdmin);

            result.Status.Should().Be(200);
            f.Store.Verify(s => s.ReplaceAllAsync(It.Is<IEnumerable<FeedGame>>(g => g.Single().ExternalId == "9")), Times.Once);
        }

        // ── Mode ─────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetMockFeedMode_ShouldReturnTheStoredMode_WhenSuperAdmin()
        {
            var result = await Call(new Fixture().Build(), "getMode", Tier.SuperAdmin);

            result.Status.Should().Be(200);
            result.Body.Should().Contain("\"mode\":\"Mock\"");
        }

        [Theory]
        [InlineData("Mock", ScoreFeedMode.Mock)]
        [InlineData("Live", ScoreFeedMode.Live)]
        public async Task SetMockFeedMode_ShouldStoreTheMode_WhenInputIsValid(string mode, ScoreFeedMode expected)
        {
            var f = new Fixture();

            var result = await Call(f.Build(), "setMode", Tier.SuperAdmin, new { mode });

            result.Status.Should().Be(200);
            f.Settings.Verify(s => s.SetModeAsync(expected), Times.Once);
        }

        [Theory]
        [InlineData("Other")]
        [InlineData("")]
        [InlineData("1")]
        [InlineData("mock")]
        public async Task SetMockFeedMode_ShouldReturn400AndStoreNothing_WhenModeIsNotLiveOrMock(string mode)
        {
            var f = new Fixture();

            var result = await Call(f.Build(), "setMode", Tier.SuperAdmin, new { mode });

            result.Status.Should().Be(400);
            f.Settings.Verify(s => s.SetModeAsync(It.IsAny<ScoreFeedMode>()), Times.Never);
        }

        [Fact]
        public async Task SetMockFeedMode_ShouldReturn400AndStoreNothing_WhenBodyIsMissing()
        {
            var f = new Fixture();

            var result = await Call(f.Build(), "setMode", Tier.SuperAdmin);

            result.Status.Should().Be(400);
            f.Settings.Verify(s => s.SetModeAsync(It.IsAny<ScoreFeedMode>()), Times.Never);
        }

        // ── Reset (only as part of switching from Mock to Live) ─────────────

        [Fact]
        public async Task PreviewMockFeedReset_ShouldReturnCountsAndChangeNothing_WhenSuperAdmin()
        {
            var f = new Fixture();

            var result = await Call(f.Build(), "preview", Tier.SuperAdmin);

            result.Status.Should().Be(200);
            result.Body.Should().Contain("\"gamesToReset\":4");
            f.Reset.Verify(r => r.ApplyAsync(It.IsAny<int>()), Times.Never);
            f.Settings.Verify(s => s.SetModeAsync(It.IsAny<ScoreFeedMode>()), Times.Never);
        }

        [Fact]
        public async Task SetMockFeedMode_ShouldSwitchToLiveThenReset_WhenModeIsMockAndCountMatches()
        {
            var f = new Fixture();
            var calls = new List<string>();
            f.Settings.Setup(s => s.SetModeAsync(ScoreFeedMode.Live)).Callback(() => calls.Add("live")).Returns(Task.CompletedTask);
            f.Reset.Setup(r => r.ApplyAsync(4)).Callback(() => calls.Add("reset"))
                .ReturnsAsync(new MockFeedResetResult(MockFeedResetStatus.Ok, new MockFeedResetSummary { GamesToReset = 4 }));

            var result = await Call(f.Build(), "setMode", Tier.SuperAdmin, new { mode = "Live", resetConfirm = 4 });

            result.Status.Should().Be(200);
            // Live first, so no refresh applies mock scores while the games are reset.
            calls.Should().Equal("live", "reset");
        }

        [Fact]
        public async Task SetMockFeedMode_ShouldReturn500WithAClearMessage_WhenTheResetFailsAfterTheSwitch()
        {
            var f = new Fixture();
            f.Reset.Setup(r => r.ApplyAsync(4)).ThrowsAsync(new InvalidOperationException("batch failed"));

            var result = await Call(f.Build(), "setMode", Tier.SuperAdmin, new { mode = "Live", resetConfirm = 4 });

            result.Status.Should().Be(500);
            result.Body.Should().Contain("now Live").And.Contain("did not finish");
            result.Body.Should().NotContain("batch failed");
            f.Settings.Verify(s => s.SetModeAsync(ScoreFeedMode.Live), Times.Once);
        }

        [Fact]
        public async Task SetMockFeedMode_ShouldRefuseTheResetAndChangeNothing_WhenModeIsNotMock()
        {
            var f = new Fixture();
            f.Settings.Setup(s => s.GetModeAsync()).ReturnsAsync(ScoreFeedMode.Live);

            var result = await Call(f.Build(), "setMode", Tier.SuperAdmin, new { mode = "Live", resetConfirm = 4 });

            result.Status.Should().Be(409);
            f.Settings.Verify(s => s.SetModeAsync(It.IsAny<ScoreFeedMode>()), Times.Never);
            f.Reset.Verify(r => r.ApplyAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task SetMockFeedMode_ShouldRefuseAndSwitchNothing_WhenTheCountChangedSincePreview()
        {
            var f = new Fixture();

            var result = await Call(f.Build(), "setMode", Tier.SuperAdmin, new { mode = "Live", resetConfirm = 9 });

            result.Status.Should().Be(409);
            f.Settings.Verify(s => s.SetModeAsync(It.IsAny<ScoreFeedMode>()), Times.Never);
            f.Reset.Verify(r => r.ApplyAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task SetMockFeedMode_ShouldReturn400AndSwitchNothing_WhenNoSeasonIsCurrent()
        {
            var f = new Fixture();
            f.Reset.Setup(r => r.PreviewAsync()).ReturnsAsync(new MockFeedResetResult(MockFeedResetStatus.NoSeason, null));

            var result = await Call(f.Build(), "setMode", Tier.SuperAdmin, new { mode = "Live", resetConfirm = 4 });

            result.Status.Should().Be(400);
            f.Settings.Verify(s => s.SetModeAsync(It.IsAny<ScoreFeedMode>()), Times.Never);
        }

        [Fact]
        public async Task SetMockFeedMode_ShouldReturn400AndChangeNothing_WhenResetIsAskedForWithMock()
        {
            var f = new Fixture();

            var result = await Call(f.Build(), "setMode", Tier.SuperAdmin, new { mode = "Mock", resetConfirm = 4 });

            result.Status.Should().Be(400);
            f.Settings.Verify(s => s.SetModeAsync(It.IsAny<ScoreFeedMode>()), Times.Never);
            f.Reset.Verify(r => r.ApplyAsync(It.IsAny<int>()), Times.Never);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData("four")]
        [InlineData(1.5)]
        public async Task SetMockFeedMode_ShouldReturn400AndChangeNothing_WhenResetConfirmIsNotAWholeNumber(object confirm)
        {
            var f = new Fixture();

            var result = await Call(f.Build(), "setMode", Tier.SuperAdmin, new { mode = "Live", resetConfirm = confirm });

            result.Status.Should().Be(400);
            f.Settings.Verify(s => s.SetModeAsync(It.IsAny<ScoreFeedMode>()), Times.Never);
            f.Reset.Verify(r => r.ApplyAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task SetMockFeedMode_ShouldSwitchWithoutResetting_WhenNoResetConfirmIsGiven()
        {
            var f = new Fixture();

            var result = await Call(f.Build(), "setMode", Tier.SuperAdmin, new { mode = "Live" });

            result.Status.Should().Be(200);
            f.Settings.Verify(s => s.SetModeAsync(ScoreFeedMode.Live), Times.Once);
            f.Reset.Verify(r => r.ApplyAsync(It.IsAny<int>()), Times.Never);
        }
    }
}
