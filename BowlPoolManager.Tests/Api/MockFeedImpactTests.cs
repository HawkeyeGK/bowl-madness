using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using BowlPoolManager.Api.Repositories;
using BowlPoolManager.Api.Services.ScoreFeed;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.ScoreFeed;

namespace BowlPoolManager.Tests.Api
{
    /// <summary>
    /// Before the mock feed is switched on, the admin sees which real games it could change. The score refresh only
    /// updates linked games that are not Final, and only when the mock has a game with the same external id.
    /// </summary>
    public class MockFeedImpactTests
    {
        private static BowlGame G(string id, string name, string? externalId, GameStatus status = GameStatus.Scheduled) =>
            new() { Id = id, BowlName = name, ExternalId = externalId, Status = status, SeasonId = "season-1" };

        [Fact]
        public void Find_ShouldCountOnlyLinkedUnfinishedGamesWhoseIdIsInTheMock_WhenGamesAreMixed()
        {
            var games = new List<BowlGame>
            {
                G("g1", "Matches Scheduled", "100"),
                G("g2", "Matches Live", "101", GameStatus.InProgress),
                G("g3", "Matches But Final", "102", GameStatus.Final),
                G("g4", "Linked No Match", "999"),
                G("g5", "Not Linked", null),
                G("g6", "Empty Link", "")
            };

            var summary = MockFeedImpact.Find(games, new HashSet<string> { "100", "101", "102" });

            summary.MatchingGames.Should().Be(2);
            summary.GameNames.Should().Equal("Matches Scheduled", "Matches Live");
            summary.LinkedGames.Should().Be(4);
        }

        [Fact]
        public void Find_ShouldReportNothing_WhenTheMockHasNoGames()
        {
            var summary = MockFeedImpact.Find(new List<BowlGame> { G("g1", "One", "100") }, new HashSet<string>());

            summary.MatchingGames.Should().Be(0);
            summary.GameNames.Should().BeEmpty();
            summary.LinkedGames.Should().Be(1);
        }

        private static (MockFeedImpactService Service, Mock<IGameRepository> Games, Mock<IMockFeedStore> Store) Service(bool currentSeason = true)
        {
            var games = new Mock<IGameRepository>();
            var seasons = new Mock<ISeasonRepository>();
            var store = new Mock<IMockFeedStore>();
            seasons.Setup(s => s.GetSeasonsAsync()).ReturnsAsync(currentSeason
                ? new List<Season> { new() { Id = "season-1", SeasonId = "2026", IsCurrent = true }, new() { Id = "old", SeasonId = "2025" } }
                : new List<Season> { new() { Id = "old", SeasonId = "2025" } });
            games.Setup(g => g.GetGamesAsync(It.IsAny<string?>())).ReturnsAsync(new List<BowlGame> { G("g1", "One", "100"), G("g2", "Two", "200") });
            store.Setup(s => s.GetAllAsync()).ReturnsAsync(new List<MockFeedGame> { new() { Id = "100", Game = new FeedGame { ExternalId = "100" } } });
            return (new MockFeedImpactService(games.Object, seasons.Object, store.Object), games, store);
        }

        [Fact]
        public async Task PreviewAsync_ShouldReadTheCurrentSeasonByIdAndWriteNothing_WhenAMockGameMatches()
        {
            var (service, games, _) = Service();

            var summary = await service.PreviewAsync();

            summary.MatchingGames.Should().Be(1);
            summary.GameNames.Should().Equal("One");
            games.Verify(g => g.GetGamesAsync("season-1"), Times.Once);
            games.Verify(g => g.UpdateGamesAsBatchAsync(It.IsAny<List<BowlGame>>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task PreviewAsync_ShouldReportNothingAndReadNoGames_WhenNoSeasonIsCurrent()
        {
            var (service, games, _) = Service(currentSeason: false);

            var summary = await service.PreviewAsync();

            summary.MatchingGames.Should().Be(0);
            games.Verify(g => g.GetGamesAsync(It.IsAny<string?>()), Times.Never);
        }
    }
}
