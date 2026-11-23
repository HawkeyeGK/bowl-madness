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
    /// <summary>Issue 133: the admin results page cannot save a Final game without two scores and a winner.</summary>
    public class SaveGameFinalRuleTests
    {
        private static (GameFunctions sut, Mock<IGameScoringService> scoring) Build()
        {
            var scoring = new Mock<IGameScoringService>();
            var sut = new GameFunctions(new NullLoggerFactory(), new Mock<IGameRepository>().Object,
                EndpointKit.Users().Object, new Mock<IScoreFeed>().Object, new Mock<ISeasonRepository>().Object, scoring.Object);
            return (sut, scoring);
        }

        private static BowlGame Game(GameStatus status, int? home, int? away) => new()
        {
            Id = "g1", SeasonId = "season-1", BowlName = "Test Bowl", TeamHome = "Home", TeamAway = "Away",
            Status = status, TeamHomeScore = home, TeamAwayScore = away
        };

        [Theory]
        [InlineData(null, null)]
        [InlineData(7, null)]
        [InlineData(null, 7)]
        [InlineData(24, 24)]
        public async Task SaveGame_ShouldRefuseAndSaveNothing_WhenFinalHasNoWinner(int? home, int? away)
        {
            var (sut, scoring) = Build();

            var result = await sut.SaveGame(EndpointKit.Request(Tier.SuperAdmin, body: Game(GameStatus.Final, home, away)));

            EndpointKit.Status(result).Should().Be(400);
            EndpointKit.BodyText(result).Should().Contain("Final");
            scoring.Verify(s => s.ProcessGameUpdateAsync(It.IsAny<BowlGame>()), Times.Never);
        }

        [Theory]
        [InlineData(GameStatus.Final, 31, 17)]
        [InlineData(GameStatus.Final, 17, 31)]
        [InlineData(GameStatus.Scheduled, null, null)]
        [InlineData(GameStatus.InProgress, 7, 7)]
        public async Task SaveGame_ShouldSave_WhenGameIsFinalWithAWinnerOrNotFinal(GameStatus status, int? home, int? away)
        {
            var (sut, scoring) = Build();

            var result = await sut.SaveGame(EndpointKit.Request(Tier.SuperAdmin, body: Game(status, home, away)));

            EndpointKit.Status(result).Should().Be(200);
            scoring.Verify(s => s.ProcessGameUpdateAsync(It.IsAny<BowlGame>()), Times.Once);
        }
    }

    /// <summary>Issue 97: the leaderboard shows names and scores only until the pool locks, so no tiebreaker guesses.</summary>
    public class LeaderboardTiebreakerVisibilityTests
    {
        private static BracketEntry Entry(string id) => new()
        {
            Id = id, SeasonId = "season-1", PoolId = "pool-1", PlayerName = "Bracket " + id,
            Picks = new Dictionary<string, string> { ["game-1"] = "Home" }, TieBreakerPoints = 77
        };

        private static JsonElement FirstRow(string body)
        {
            var doc = JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("leaderboard")[0];
        }

        [Fact]
        public async Task GetLeaderboard_ShouldHideTiebreakers_WhenPoolIsNotLocked()
        {
            var games = new Mock<IGameRepository>();
            games.Setup(g => g.GetGamesAsync(It.IsAny<string?>())).ReturnsAsync(new List<BowlGame>());
            var entries = new Mock<IEntryRepository>();
            entries.Setup(e => e.GetEntriesAsync(It.IsAny<string?>(), It.IsAny<string?>())).ReturnsAsync(new List<BracketEntry> { Entry("e1") });
            var pools = new Mock<IPoolRepository>();
            pools.Setup(p => p.GetPoolAsync("pool-1")).ReturnsAsync(new BowlPool { Id = "pool-1", SeasonId = "season-1", LockDate = DateTime.UtcNow.AddDays(3) });
            var sut = new LeaderboardFunctions(new NullLoggerFactory(), games.Object, entries.Object, pools.Object);

            var result = await sut.GetLeaderboard(EndpointKit.Request(Tier.Anonymous, "?poolId=pool-1&seasonId=season-1"));

            var row = FirstRow(EndpointKit.BodyText(result));
            row.GetProperty("playerName").GetString().Should().Be("Bracket e1");
            row.GetProperty("tieBreakerPoints").GetInt32().Should().Be(0);
        }

        [Fact]
        public async Task GetLeaderboard_ShouldShowTiebreakers_WhenPoolIsLocked()
        {
            var games = new Mock<IGameRepository>();
            games.Setup(g => g.GetGamesAsync(It.IsAny<string?>())).ReturnsAsync(new List<BowlGame>());
            var entries = new Mock<IEntryRepository>();
            entries.Setup(e => e.GetEntriesAsync(It.IsAny<string?>(), It.IsAny<string?>())).ReturnsAsync(new List<BracketEntry> { Entry("e1") });
            var pools = new Mock<IPoolRepository>();
            pools.Setup(p => p.GetPoolAsync("pool-1")).ReturnsAsync(new BowlPool { Id = "pool-1", SeasonId = "season-1", LockDate = DateTime.UtcNow.AddDays(-1) });
            var sut = new LeaderboardFunctions(new NullLoggerFactory(), games.Object, entries.Object, pools.Object);

            var result = await sut.GetLeaderboard(EndpointKit.Request(Tier.Anonymous, "?poolId=pool-1&seasonId=season-1"));

            FirstRow(EndpointKit.BodyText(result)).GetProperty("tieBreakerPoints").GetInt32().Should().Be(77);
        }

        [Fact]
        public async Task GetHoopsLeaderboard_ShouldHideTiebreakers_WhenPoolIsNotLocked()
        {
            var games = new Mock<IHoopsGameRepository>();
            games.Setup(g => g.GetGamesAsync(It.IsAny<string>())).ReturnsAsync(new List<HoopsGame>());
            var entries = new Mock<IHoopsEntryRepository>();
            entries.Setup(e => e.GetEntriesAsync(It.IsAny<string?>(), It.IsAny<string?>())).ReturnsAsync(new List<BracketEntry> { Entry("e1") });
            var pools = new Mock<IHoopsPoolRepository>();
            pools.Setup(p => p.GetPoolAsync("pool-1")).ReturnsAsync(new HoopsPool { Id = "pool-1", SeasonId = "season-1", LockDate = DateTime.UtcNow.AddDays(3) });
            var sut = new HoopsLeaderboardFunctions(new NullLoggerFactory(), games.Object, entries.Object, pools.Object);

            var result = await sut.GetHoopsLeaderboard(EndpointKit.Request(Tier.Anonymous, "?poolId=pool-1&seasonId=season-1"));

            FirstRow(EndpointKit.BodyText(result)).GetProperty("tieBreakerPoints").GetInt32().Should().Be(0);
        }

        [Fact]
        public async Task GetHoopsLeaderboard_ShouldShowTiebreakers_WhenPoolIsLocked()
        {
            var games = new Mock<IHoopsGameRepository>();
            games.Setup(g => g.GetGamesAsync(It.IsAny<string>())).ReturnsAsync(new List<HoopsGame>());
            var entries = new Mock<IHoopsEntryRepository>();
            entries.Setup(e => e.GetEntriesAsync(It.IsAny<string?>(), It.IsAny<string?>())).ReturnsAsync(new List<BracketEntry> { Entry("e1") });
            var pools = new Mock<IHoopsPoolRepository>();
            pools.Setup(p => p.GetPoolAsync("pool-1")).ReturnsAsync(new HoopsPool { Id = "pool-1", SeasonId = "season-1", LockDate = DateTime.UtcNow.AddDays(-1) });
            var sut = new HoopsLeaderboardFunctions(new NullLoggerFactory(), games.Object, entries.Object, pools.Object);

            var result = await sut.GetHoopsLeaderboard(EndpointKit.Request(Tier.Anonymous, "?poolId=pool-1&seasonId=season-1"));

            FirstRow(EndpointKit.BodyText(result)).GetProperty("tieBreakerPoints").GetInt32().Should().Be(77);
        }
    }
}
