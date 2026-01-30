using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Api.Repositories
{
    public interface IPoolRepository
    {
        Task AddPoolAsync(BowlPool pool);
        Task DeletePoolAsync(string poolId);
        Task<List<BowlPool>> GetPoolsAsync(string? seasonId = null);
        Task<BowlPool?> GetPoolAsync(string id);
        Task<BowlPool?> GetPoolByInviteCodeAsync(string inviteCode);
    }
}
