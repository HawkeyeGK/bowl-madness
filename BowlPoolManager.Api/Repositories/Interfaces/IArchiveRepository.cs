using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Api.Repositories
{
    public interface IArchiveRepository
    {
        Task AddArchiveAsync(PoolArchive archive);
        Task<PoolArchive?> GetArchiveAsync(string id);
        Task<List<PoolArchive>> GetArchivesBySeasonAsync(string seasonId);
    }
}
