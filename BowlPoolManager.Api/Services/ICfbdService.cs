using BowlPoolManager.Core.Dtos;

namespace BowlPoolManager.Api.Services
{
    public interface ICfbdService
    {
        Task<List<CfbdGameDto>> GetPostseasonGamesAsync(int year);
    }
}
