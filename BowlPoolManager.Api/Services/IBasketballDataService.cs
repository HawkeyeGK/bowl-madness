using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Api.Services
{
    public interface IBasketballDataService
    {
        Task<List<TeamInfo>> GetTeamsAsync();
    }
}
