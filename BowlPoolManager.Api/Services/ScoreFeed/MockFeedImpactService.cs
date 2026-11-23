using BowlPoolManager.Api.Repositories;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.ScoreFeed;

namespace BowlPoolManager.Api.Services.ScoreFeed
{
    public interface IMockFeedImpactService
    {
        /// <summary>Read only: which linked games of the current season the mock would update if it were switched on.</summary>
        Task<MockFeedEnableSummary> PreviewAsync();
    }

    public static class MockFeedImpact
    {
        // GameScoringService only updates linked games that are not Final, and only when the feed has the same external id.
        public static MockFeedEnableSummary Find(IReadOnlyList<BowlGame> games, ISet<string> mockIds)
        {
            var linked = games.Where(g => !string.IsNullOrEmpty(g.ExternalId)).ToList();
            var matching = linked.Where(g => g.Status != GameStatus.Final && mockIds.Contains(g.ExternalId!)).ToList();
            return new MockFeedEnableSummary
            {
                MatchingGames = matching.Count,
                GameNames = matching.Select(g => g.BowlName).ToList(),
                LinkedGames = linked.Count
            };
        }
    }

    /// <summary>Reads the current season's games (partition key = the season Id) and the mock's game ids. Writes nothing.</summary>
    public class MockFeedImpactService : IMockFeedImpactService
    {
        private readonly IGameRepository _gameRepo;
        private readonly ISeasonRepository _seasonRepo;
        private readonly IMockFeedStore _store;

        public MockFeedImpactService(IGameRepository gameRepo, ISeasonRepository seasonRepo, IMockFeedStore store)
        {
            _gameRepo = gameRepo;
            _seasonRepo = seasonRepo;
            _store = store;
        }

        public async Task<MockFeedEnableSummary> PreviewAsync()
        {
            var season = (await _seasonRepo.GetSeasonsAsync()).FirstOrDefault(s => s.IsCurrent);
            if (season == null) return new MockFeedEnableSummary();

            var games = await _gameRepo.GetGamesAsync(season.Id);
            var mockIds = (await _store.GetAllAsync()).Select(m => m.Game.ExternalId).ToHashSet(StringComparer.Ordinal);
            return MockFeedImpact.Find(games, mockIds);
        }
    }
}
