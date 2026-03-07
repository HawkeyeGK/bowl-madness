using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Api.Repositories
{
    public interface IHoopsPoolRepository
    {
        Task AddPoolAsync(HoopsPool pool);
        Task DeletePoolAsync(string poolId);
        Task<List<HoopsPool>> GetPoolsAsync(string? seasonId = null);
        Task<HoopsPool?> GetPoolAsync(string id);
        Task<HoopsPool?> GetPoolByInviteCodeAsync(string inviteCode);
    }
}
