using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using BowlPoolManager.Api.Repositories;
using BowlPoolManager.Api.Services;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Api.Services.ScoreFeed;

namespace BowlPoolManager.Tests.Api
{
    /// <summary>
    /// Issue 130: a corrected or reversed first-round result must fill the slot its feeder owns and
    /// leave the seeded team alone.
    /// </summary>
    public class GameScoringServicePropagationTests
    {
        private readonly Mock<IGameRepository> _repo = new();
        private readonly List<BowlGame> _season = new();
        private readonly GameScoringService _service;

        public GameScoringServicePropagationTests()
        {
            _repo.Setup(r => r.GetGamesAsync(It.IsAny<string>())).ReturnsAsync(() => _season);
            _repo.Setup(r => r.UpdateGamesAsBatchAsync(It.IsAny<List<BowlGame>>(), It.IsAny<string>())).Returns(Task.CompletedTask);
            _service = new GameScoringService(NullLogger<GameScoringService>.Instance, _repo.Object, Mock.Of<IScoreFeed>());
        }

        // A first-round game (Miami v Texas A&M, Miami won) feeding a quarterfinal whose home slot is the #1 seed.
        private (BowlGame firstRound, BowlGame quarter) SeasonWithFirstRoundWinnerPropagated()
        {
            var quarter = new BowlGame
            {
                Id = "qf", SeasonId = "season-1", BowlName = "Quarterfinal", StartTime = new DateTime(2026, 12, 31, 18, 0, 0, DateTimeKind.Utc),
                TeamHome = "Ohio State", TeamHomeSeed = 1, TeamAway = "Winner of First Round", IsPlayoff = true, Round = TournamentRound.QuarterFinal
            };
            var firstRound = new BowlGame
            {
                Id = "r1", SeasonId = "season-1", BowlName = "First Round", StartTime = new DateTime(2026, 12, 20, 18, 0, 0, DateTimeKind.Utc),
                TeamHome = "Miami", TeamAway = "Texas A&M", NextGameId = "qf", IsPlayoff = true, Round = TournamentRound.Round1
            };
            _season.AddRange(new[] { firstRound, quarter });
            return (firstRound, quarter);
        }

        private static BowlGame Copy(BowlGame g, GameStatus status, int? home, int? away) => new()
        {
            Id = g.Id, SeasonId = g.SeasonId, BowlName = g.BowlName, StartTime = g.StartTime, TeamHome = g.TeamHome, TeamAway = g.TeamAway,
            NextGameId = g.NextGameId, IsPlayoff = g.IsPlayoff, Round = g.Round, Status = status, TeamHomeScore = home, TeamAwayScore = away
        };

        [Fact]
        public async Task ProcessGameUpdateAsync_ShouldFillTheAwaySlot_WhenFirstRoundFinalIsFirstEntered()
        {
            var (firstRound, quarter) = SeasonWithFirstRoundWinnerPropagated();

            await _service.ProcessGameUpdateAsync(Copy(firstRound, GameStatus.Final, 24, 17));

            quarter.TeamHome.Should().Be("Ohio State");
            quarter.TeamAway.Should().Be("Miami");
        }

        [Fact]
        public async Task ProcessGameUpdateAsync_ShouldKeepTheSeed_WhenFirstRoundResultIsCorrected()
        {
            var (firstRound, quarter) = SeasonWithFirstRoundWinnerPropagated();
            await _service.ProcessGameUpdateAsync(Copy(firstRound, GameStatus.Final, 24, 17)); // Miami wins, copied to away

            await _service.ProcessGameUpdateAsync(Copy(firstRound, GameStatus.Final, 17, 24)); // corrected: Texas A&M won

            quarter.TeamHome.Should().Be("Ohio State");
            quarter.TeamHomeSeed.Should().Be(1);
            quarter.TeamAway.Should().Be("Texas A&M");
        }

        [Fact]
        public async Task ProcessGameUpdateAsync_ShouldKeepTheSeed_WhenFirstRoundFinalGoesBackToScheduled()
        {
            var (firstRound, quarter) = SeasonWithFirstRoundWinnerPropagated();
            await _service.ProcessGameUpdateAsync(Copy(firstRound, GameStatus.Final, 24, 17));

            await _service.ProcessGameUpdateAsync(Copy(firstRound, GameStatus.Scheduled, null, null));

            quarter.TeamHome.Should().Be("Ohio State");
            quarter.TeamHomeSeed.Should().Be(1);
            quarter.TeamAway.Should().Be("Winner of First Round");
        }

        [Fact]
        public async Task ForcePropagateAllAsync_ShouldKeepTheSeed_WhenRunTwiceAfterACorrection()
        {
            var (firstRound, quarter) = SeasonWithFirstRoundWinnerPropagated();
            firstRound.Status = GameStatus.Final; firstRound.TeamHomeScore = 24; firstRound.TeamAwayScore = 17;
            await _service.ForcePropagateAllAsync("season-1");
            firstRound.TeamHomeScore = 17; firstRound.TeamAwayScore = 24;

            await _service.ForcePropagateAllAsync("season-1");

            quarter.TeamHome.Should().Be("Ohio State");
            quarter.TeamAway.Should().Be("Texas A&M");
        }
    }
}
