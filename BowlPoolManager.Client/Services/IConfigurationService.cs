using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Client.Services
{
    public interface IConfigurationService
    {
        Task<List<TeamInfo>> GetTeamsAsync();
    }
}
