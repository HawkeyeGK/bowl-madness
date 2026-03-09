using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Client.Services
{
    public interface IHoopsPoolService
    {
        Task<List<HoopsPool>> GetPoolsAsync(string? seasonId = null);
        Task<HoopsPool?> GetPoolAsync(string poolId);
        Task<HoopsPool?> CreatePoolAsync(HoopsPool pool);
        Task<HoopsPool?> UpdatePoolAsync(HoopsPool pool);
        Task<bool> DeletePoolAsync(string poolId);
        Task<HoopsPool?> ToggleConclusionAsync(string poolId);
        Task<bool> ArchivePoolAsync(string poolId);
        Task<PoolArchive?> GetArchiveAsync(string poolId);
    }
}
