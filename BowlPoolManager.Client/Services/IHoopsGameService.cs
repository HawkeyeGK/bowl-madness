using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Dtos;

namespace BowlPoolManager.Client.Services
{
    public interface IHoopsGameService
    {
        Task<List<HoopsGame>> GetGamesAsync(string poolId);
        Task<List<HoopsGame>?> GenerateBracketAsync(BracketGenerationRequest request);
        Task<HoopsGame?> UpdateGameAsync(HoopsGame game);
        Task<bool> SaveTeamAssignmentsAsync(List<HoopsGame> games);
    }
}
