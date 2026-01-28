using BowlPoolManager.Core.Dtos;

namespace BowlPoolManager.Api.Repositories
{
    public interface IMigrationRepository
    {
        Task<List<LegacyGameDto>> GetLegacyGamesAsync();
        Task<List<string>> GetLegacyTeamNamesAsync();
        Task<(List<LegacyGameDto> Games, List<string> TeamNames, List<string> SeasonIds, List<LegacyPoolDto> Pools, int EntryCount)> AnalyzeLegacyDataAsync();
        Task<List<dynamic>> GetLegacyEntriesAsync();
    }
}
