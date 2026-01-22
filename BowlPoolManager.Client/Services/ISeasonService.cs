using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Client.Services
{
    public interface ISeasonService
    {
        Task<List<Season>> GetSeasonsAsync();
        Task UpsertSeasonAsync(Season season);
    }
}
