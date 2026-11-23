using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using BowlPoolManager.Api.Repositories;
using BowlPoolManager.Api.Services;
using BowlPoolManager.Api.Services.ScoreFeed;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.ScoreFeed;

namespace BowlPoolManager.Tests.Api
{
    /// <summary>
    /// GameScoringService driven by the mock feed: status changes, scores, swapped teams and a final whose names
    /// do not match the link (which sets up issue 133 in group B).
    /// </summary>
    public class GameScoringServiceMockFeedTests
    {
        private class InMemoryMockFeedStore : IMockFeedStore
        {
            public List<MockFeedGame> Games { get; } = new();
            public Task<List<MockFeedGame>> GetAllAsync() => Task.FromResult(Games.ToList());
            public Task UpsertAsync(MockFeedGame game) { Games.RemoveAll(g => g.Id == game.Id); Games.Add(game); return Task.CompletedTask; }
            public Task DeleteAsync(string id) { Games.RemoveAll(g => g.Id == id); return Task.CompletedTask; }
            public Task<int> ReplaceAllAsync(IEnumerable<FeedGame> games) => throw new NotSupportedException();
        }

        private readonly InMemoryMockFeedStore _store = new();
        private readonly Mock<IGameRepository> _repo = new();
        private readonly List<BowlGame> _saved = new();
        private readonly GameScoringService _service;

        public GameScoringServiceMockFeedTests()
        {
            // The service throttles refreshes with a static timestamp; reset it so each test runs a refresh.
            typeof(GameScoringService).GetField("_lastRefresh", BindingFlags.NonPublic | BindingFlags.Static)!
                .SetValue(null, DateTime.MinValue);

            _repo.Setup(r => r.UpdateGamesAsBatchAsync(It.IsAny<List<BowlGame>>(), It.IsAny<string>()))
                .Callback((List<BowlGame> games, string _) => _saved.AddRange(games))
                .Returns(Task.CompletedTask);

            _service = new GameScoringService(NullLogger<GameScoringService>.Instance, _repo.Object,
                new MockScoreFeed(_store));
        }

        private static BowlGame Local(string id = "g1", string externalId = "100") => new()
        {
            Id = id, SeasonId = "season-1", BowlName = "Test Bowl", ExternalId = externalId,
            TeamHome = "Home State", TeamAway = "Away Tech",
            ApiHomeTeam = "Home State", ApiAwayTeam = "Away Tech",
            StartTime = DateTime.UtcNow.AddDays(10) // far ahead, so the start-time rule does not mark it in progress
        };

        private Task Feed(FeedGameStatus status, string home, int? homePts, string away, int? awayPts,
            int? period = null, string? clock = null, string id = "100") =>
            _store.UpsertAsync(new MockFeedGame
            {
                Id = id,
                Game = new FeedGame
                {
                    ExternalId = id, Status = status, Period = period, Clock = clock,
                    Home = new FeedTeam { Name = home, Points = homePts }, Away = new FeedTeam { Name = away, Points = awayPts }
                }
            });

        [Fact]
        public async Task CheckAndRefreshScoresAsync_ShouldSetInProgressWithScoresAndDetail_WhenFeedReportsALiveGame()
        {
            var game = Local();
            await Feed(FeedGameStatus.InProgress, "Home State", 10, "Away Tech", 3, period: 2, clock: "08:15");

            await _service.CheckAndRefreshScoresAsync(new List<BowlGame> { game });

            game.Status.Should().Be(GameStatus.InProgress);
            game.GameDetail.Should().Be("2nd • 08:15");
            game.TeamHomeScore.Should().Be(10);
            game.TeamAwayScore.Should().Be(3);
            _saved.Should().Contain(game);
        }

        [Theory]
        [InlineData(1, "1st")]
        [InlineData(4, "4th")]
        [InlineData(5, "OT")]
        public async Task CheckAndRefreshScoresAsync_ShouldLabelThePeriod_WhenFeedReportsOne(int period, string label)
        {
            var game = Local();
            await Feed(FeedGameStatus.InProgress, "Home State", 0, "Away Tech", 0, period: period, clock: "01:00");

            await _service.CheckAndRefreshScoresAsync(new List<BowlGame> { game });

            game.GameDetail.Should().Be($"{label} • 01:00");
        }

        [Fact]
        public async Task CheckAndRefreshScoresAsync_ShouldSetFinalWithScores_WhenFeedReportsFinal()
        {
            var game = Local();
            await Feed(FeedGameStatus.Final, "Home State", 27, "Away Tech", 24);

            await _service.CheckAndRefreshScoresAsync(new List<BowlGame> { game });

            game.Status.Should().Be(GameStatus.Final);
            game.GameDetail.Should().Be("Final");
            game.TeamHomeScore.Should().Be(27);
            game.TeamAwayScore.Should().Be(24);
            game.WinningTeamName.Should().Be("Home State");
        }

        [Fact]
        public async Task CheckAndRefreshScoresAsync_ShouldPutScoresOnTheRightTeams_WhenFeedSwapsHomeAndAway()
        {
            var game = Local();
            await Feed(FeedGameStatus.Final, "Away Tech", 31, "Home State", 14);

            await _service.CheckAndRefreshScoresAsync(new List<BowlGame> { game });

            game.TeamHomeScore.Should().Be(14);
            game.TeamAwayScore.Should().Be(31);
            game.WinningTeamName.Should().Be("Away Tech");
        }

        [Fact]
        public async Task CheckAndRefreshScoresAsync_ShouldMatchTeamNamesIgnoringCase_WhenFeedSpellsThemDifferently()
        {
            var game = Local();
            await Feed(FeedGameStatus.Final, "HOME STATE", 3, "away tech", 0);

            await _service.CheckAndRefreshScoresAsync(new List<BowlGame> { game });

            game.TeamHomeScore.Should().Be(3);
            game.TeamAwayScore.Should().Be(0);
        }

        [Fact]
        public async Task CheckAndRefreshScoresAsync_ShouldFillTheNextGame_WhenAFeederGoesFinal()
        {
            var feeder = Local("g1", "100");
            feeder.NextGameId = "g2";
            var next = new BowlGame { Id = "g2", SeasonId = "season-1", TeamHome = "Winner of Test Bowl", TeamAway = "Other U" };
            await Feed(FeedGameStatus.Final, "Home State", 20, "Away Tech", 10);

            await _service.CheckAndRefreshScoresAsync(new List<BowlGame> { feeder, next });

            next.TeamHome.Should().Be("Home State");
            _saved.Should().Contain(next);
        }

        [Fact]
        public async Task CheckAndRefreshScoresAsync_ShouldNotTouchAGame_WhenTheFeedDoesNotHaveIt()
        {
            var game = Local(externalId: "999");
            await Feed(FeedGameStatus.Final, "Home State", 20, "Away Tech", 10, id: "100");

            await _service.CheckAndRefreshScoresAsync(new List<BowlGame> { game });

            game.Status.Should().Be(GameStatus.Scheduled);
            _saved.Should().BeEmpty();
        }

        [Fact]
        public async Task CheckAndRefreshScoresAsync_ShouldNotTouchAGame_WhenItIsNotLinkedOrAlreadyFinal()
        {
            var unlinked = Local("g1", externalId: "");
            var finished = Local("g2", "100");
            finished.Status = GameStatus.Final;
            finished.TeamHomeScore = 1;
            finished.TeamAwayScore = 0;
            await Feed(FeedGameStatus.Final, "Home State", 50, "Away Tech", 49);

            await _service.CheckAndRefreshScoresAsync(new List<BowlGame> { unlinked, finished });

            unlinked.Status.Should().Be(GameStatus.Scheduled);
            finished.TeamHomeScore.Should().Be(1);
            _saved.Should().BeEmpty();
        }

        [Fact]
        public async Task CheckAndRefreshScoresAsync_ShouldMarkInProgress_WhenKickoffIsWithin15MinutesAndFeedStillSaysScheduled()
        {
            var game = Local();
            game.StartTime = DateTime.UtcNow.AddMinutes(-5);
            await Feed(FeedGameStatus.Scheduled, "Home State", null, "Away Tech", null);

            await _service.CheckAndRefreshScoresAsync(new List<BowlGame> { game });

            game.Status.Should().Be(GameStatus.InProgress);
            game.GameDetail.Should().Be("In Progress");
        }

        // #133. A final whose team names do not match the link is marked Final with no scores,
        // and the leaderboard then awards it to the away team.
        [Fact]
        public async Task CheckAndRefreshScoresAsync_ShouldNotMarkFinal_WhenFinalGameNamesDoNotMatchTheLink()
        {
            var game = Local();
            await Feed(FeedGameStatus.Final, "Home State University", 27, "Away Technical", 24);

            await _service.CheckAndRefreshScoresAsync(new List<BowlGame> { game });

            game.Status.Should().NotBe(GameStatus.Final);
        }

        // Issue 133: a Final needs both scores matched and a winner. A tie, or a score the feed does not give, is not usable.
        [Theory]
        [InlineData(20, 20)]
        [InlineData(null, 20)]
        [InlineData(20, null)]
        public async Task CheckAndRefreshScoresAsync_ShouldNotMarkFinal_WhenFinalHasNoWinner(int? homePoints, int? awayPoints)
        {
            var game = Local();
            await Feed(FeedGameStatus.Final, "Home State", homePoints, "Away Tech", awayPoints);

            await _service.CheckAndRefreshScoresAsync(new List<BowlGame> { game });

            game.Status.Should().NotBe(GameStatus.Final);
            game.WinningTeamName.Should().BeNull();
        }

        [Fact]
        public async Task CheckAndRefreshScoresAsync_ShouldMarkFinalWithWinner_WhenFinalNamesMatchAndScoresDiffer()
        {
            var game = Local();
            await Feed(FeedGameStatus.Final, "Away Tech", 31, "Home State", 17); // feed lists the teams the other way round

            await _service.CheckAndRefreshScoresAsync(new List<BowlGame> { game });

            game.Status.Should().Be(GameStatus.Final);
            game.TeamHomeScore.Should().Be(17);
            game.TeamAwayScore.Should().Be(31);
            game.WinningTeamName.Should().Be("Away Tech");
        }
    }
}
