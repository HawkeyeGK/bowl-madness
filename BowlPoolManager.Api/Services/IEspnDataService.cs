using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Api.Services
{
    public interface IEspnDataService
    {
        Task<List<TeamInfo>> GetTeamsAsync();
    }
}
