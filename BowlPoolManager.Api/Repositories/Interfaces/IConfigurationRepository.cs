using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Api.Repositories
{
    public interface IConfigurationRepository
    {
        Task<TeamConfig?> GetTeamConfigAsync();
    }
}
