using Microsoft.Extensions.Logging;
using BowlPoolManager.Api.Repositories;
using BowlPoolManager.Core.ScoreFeed;

namespace BowlPoolManager.Api.Services.ScoreFeed
{
    public enum MockFeedResetStatus { Ok, NoSeason, CountChanged }

    public record MockFeedResetResult(MockFeedResetStatus Status, MockFeedResetSummary? Summary);

    public interface IMockFeedResetService
    {
        /// <summary>Counts only; changes nothing.</summary>
        Task<MockFeedResetResult> PreviewAsync();

        /// <summary>Resets, but only when <paramref name="confirmCount"/> equals the number of games the preview would reset.</summary>
        Task<MockFeedResetResult> ApplyAsync(int confirmCount);
    }

    /// <summary>
    /// Live-data bulk write: games of the CURRENT season only, read and saved with that season's Id as the
    /// partition key, in batches of at most 100 (the Cosmos transactional batch limit).
    /// </summary>
    public class MockFeedResetService : IMockFeedResetService
    {
        private const int BatchSize = 100;

        private readonly IGameRepository _gameRepo;
        private readonly ISeasonRepository _seasonRepo;
        private readonly ILogger<MockFeedResetService> _logger;

        public MockFeedResetService(IGameRepository gameRepo, ISeasonRepository seasonRepo, ILogger<MockFeedResetService> logger)
        {
            _gameRepo = gameRepo;
            _seasonRepo = seasonRepo;
            _logger = logger;
        }

        public async Task<MockFeedResetResult> PreviewAsync()
        {
            var season = (await _seasonRepo.GetSeasonsAsync()).FirstOrDefault(s => s.IsCurrent);
            if (season == null) return new MockFeedResetResult(MockFeedResetStatus.NoSeason, null);

            var games = await _gameRepo.GetGamesAsync(season.Id);
            return new MockFeedResetResult(MockFeedResetStatus.Ok, MockFeedReset.Build(games).ToSummary());
        }

        public async Task<MockFeedResetResult> ApplyAsync(int confirmCount)
        {
            var season = (await _seasonRepo.GetSeasonsAsync()).FirstOrDefault(s => s.IsCurrent);
            if (season == null) return new MockFeedResetResult(MockFeedResetStatus.NoSeason, null);

            var games = await _gameRepo.GetGamesAsync(season.Id);
            var plan = MockFeedReset.Build(games);
            if (plan.GameCount != confirmCount)
                return new MockFeedResetResult(MockFeedResetStatus.CountChanged, plan.ToSummary());

            // Start time and count go in the log first: they are the restore point reference if this needs undoing.
            _logger.LogInformation("Mock feed reset starting at {StartedUtc}: {Games} games, season {SeasonId}.", DateTime.UtcNow, plan.GameCount, season.Id);
            var changed = MockFeedReset.Apply(plan, games);
            foreach (var batch in changed.Chunk(BatchSize))
                await _gameRepo.UpdateGamesAsBatchAsync(batch.ToList(), season.Id);

            _logger.LogInformation("Mock feed reset finished at {FinishedUtc}: {Games} games reset, {Slots} later-round slots restored, season {SeasonId}.",
                DateTime.UtcNow, plan.GameCount, plan.SlotClears.Count, season.Id);
            return new MockFeedResetResult(MockFeedResetStatus.Ok, plan.ToSummary());
        }
    }
}
