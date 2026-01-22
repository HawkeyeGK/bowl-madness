using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Client.Services
{
    public interface IPoolService
    {
        Task<List<BowlPool>> GetPoolsAsync();
        Task<BowlPool?> GetPoolAsync(string poolId);
        Task<BowlPool?> CreatePoolAsync(BowlPool pool);
        Task<BowlPool?> ToggleConclusionAsync(string poolId); 
        Task<bool> ArchivePoolAsync(string poolId);
    }
}
