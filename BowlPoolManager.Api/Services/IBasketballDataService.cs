using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Api.Services
{
    public interface IBasketballDataService
    {
        /// <summary>
        /// Fetches team metadata (name, logos, colors, conference) from CollegeBasketballData.
        /// </summary>
        Task<List<TeamInfo>> GetTeamsAsync();
    }
}
