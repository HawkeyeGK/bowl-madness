using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Client.Services
{
    public interface IPoolService
    {
        Task<List<BowlPool>> GetPoolsAsync(string? seasonId = null);
        Task<BowlPool?> GetPoolAsync(string poolId);
        // The write methods (create, update, delete, toggle conclusion, archive) throw HttpRequestException carrying the
        // server's message when the server refuses; they do not return null or false on failure.
        Task<BowlPool?> CreatePoolAsync(BowlPool pool);

        Task<BowlPool?> UpdatePoolAsync(BowlPool pool);
        Task<bool> DeletePoolAsync(string poolId);
        Task<BowlPool?> ToggleConclusionAsync(string poolId); 
        Task<bool> ArchivePoolAsync(string poolId);
        Task<PoolArchive?> GetArchiveAsync(string poolId);
        Task<List<BowlGame>> GetGamesAsync(string? seasonId = null);
    }
}
