using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Api.Repositories
{
    public interface ISeasonRepository
    {
        Task<List<Season>> GetSeasonsAsync();
        Task UpsertSeasonAsync(Season season);
    }
}
