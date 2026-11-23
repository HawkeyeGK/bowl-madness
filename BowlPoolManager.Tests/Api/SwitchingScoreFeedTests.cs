using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using BowlPoolManager.Api.Services.ScoreFeed;
using BowlPoolManager.Core.ScoreFeed;

namespace BowlPoolManager.Tests.Api
{
    /// <summary>
    /// The feed in use is chosen by the stored mode, read on every call. Missing or unreadable means Live,
    /// so a fault can never switch a site onto mock data.
    /// </summary>
    public class SwitchingScoreFeedTests
    {
        private readonly Mock<IScoreFeed> _live = new();
        private readonly Mock<IScoreFeed> _mock = new();
        private readonly Mock<IScoreFeedSettingsStore> _settings = new();

        public SwitchingScoreFeedTests()
        {
            _live.Setup(f => f.GetScoreboardAsync(It.IsAny<DateOnly?>())).ReturnsAsync(new List<FeedGame> { new() { ExternalId = "live" } });
            _mock.Setup(f => f.GetScoreboardAsync(It.IsAny<DateOnly?>())).ReturnsAsync(new List<FeedGame> { new() { ExternalId = "mock" } });
            _live.Setup(f => f.GetPostseasonGamesAsync(It.IsAny<int>())).ReturnsAsync(new List<FeedGame> { new() { ExternalId = "live" } });
            _mock.Setup(f => f.GetPostseasonGamesAsync(It.IsAny<int>())).ReturnsAsync(new List<FeedGame> { new() { ExternalId = "mock" } });
        }

        private SwitchingScoreFeed Build() =>
            new(_live.Object, _mock.Object, _settings.Object, NullLogger<SwitchingScoreFeed>.Instance);

        [Fact]
        public async Task GetScoreboardAsync_ShouldUseLive_WhenModeIsLive()
        {
            _settings.Setup(s => s.GetModeAsync()).ReturnsAsync(ScoreFeedMode.Live);

            (await Build().GetScoreboardAsync()).Single().ExternalId.Should().Be("live");
        }

        [Fact]
        public async Task GetScoreboardAsync_ShouldUseMock_WhenModeIsMock()
        {
            _settings.Setup(s => s.GetModeAsync()).ReturnsAsync(ScoreFeedMode.Mock);

            (await Build().GetScoreboardAsync()).Single().ExternalId.Should().Be("mock");
            (await Build().GetPostseasonGamesAsync(2026)).Single().ExternalId.Should().Be("mock");
            _live.Verify(f => f.GetScoreboardAsync(It.IsAny<DateOnly?>()), Times.Never);
        }

        [Fact]
        public async Task GetScoreboardAsync_ShouldUseLiveAndNotThrow_WhenTheModeCannotBeRead()
        {
            _settings.Setup(s => s.GetModeAsync()).ThrowsAsync(new InvalidOperationException("database down"));

            (await Build().GetScoreboardAsync()).Single().ExternalId.Should().Be("live");
            (await Build().GetPostseasonGamesAsync(2026)).Single().ExternalId.Should().Be("live");
            _mock.Verify(f => f.GetScoreboardAsync(It.IsAny<DateOnly?>()), Times.Never);
        }

        [Fact]
        public async Task GetScoreboardAsync_ShouldFollowTheNewMode_WhenTheModeChangesBetweenCalls()
        {
            var modes = new Queue<ScoreFeedMode>(new[] { ScoreFeedMode.Mock, ScoreFeedMode.Live });
            _settings.Setup(s => s.GetModeAsync()).ReturnsAsync(() => modes.Dequeue());
            var feed = Build();

            (await feed.GetScoreboardAsync()).Single().ExternalId.Should().Be("mock");
            (await feed.GetScoreboardAsync()).Single().ExternalId.Should().Be("live");
        }

        [Fact]
        public async Task GetRawPostseasonJsonAsync_ShouldReturnTheProvidersOwnJson_WhenModeIsLiveAndFeedHasIt()
        {
            var live = new Mock<IScoreFeed>();
            live.As<IRawPostseasonJsonSource>().Setup(r => r.GetRawPostseasonJsonAsync(2026)).ReturnsAsync("[{\"raw\":true}]");
            _settings.Setup(s => s.GetModeAsync()).ReturnsAsync(ScoreFeedMode.Live);
            var feed = new SwitchingScoreFeed(live.Object, _mock.Object, _settings.Object, NullLogger<SwitchingScoreFeed>.Instance);

            (await feed.GetRawPostseasonJsonAsync(2026)).Should().Be("[{\"raw\":true}]");
        }

        [Fact]
        public async Task GetRawPostseasonJsonAsync_ShouldReturnMockGamesInTheCfbdShape_WhenModeIsMock()
        {
            _mock.Setup(f => f.GetPostseasonGamesAsync(It.IsAny<int>())).ReturnsAsync(new List<FeedGame>
            {
                new() { ExternalId = "42", Home = new FeedTeam { Name = "A" }, Away = new FeedTeam { Name = "B" } }
            });
            _settings.Setup(s => s.GetModeAsync()).ReturnsAsync(ScoreFeedMode.Mock);

            var json = await Build().GetRawPostseasonJsonAsync(2026);

            using var doc = JsonDocument.Parse(json);
            doc.RootElement[0].GetProperty("id").GetInt32().Should().Be(42);
            doc.RootElement[0].GetProperty("homeTeam").GetProperty("name").GetString().Should().Be("A");
        }
    }
}
