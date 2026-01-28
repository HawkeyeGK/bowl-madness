using Microsoft.Azure.Cosmos;
using BowlPoolManager.Api.Infrastructure;
using BowlPoolManager.Core.Dtos;
using BowlPoolManager.Core;
using Newtonsoft.Json;

namespace BowlPoolManager.Api.Repositories
{
    public class MigrationRepository : CosmosRepositoryBase, IMigrationRepository
    {
        private const string LegacyContainerName = "MainContainer";

        public MigrationRepository(CosmosClient cosmosClient) : base(cosmosClient, LegacyContainerName) { }

        public async Task<List<LegacyGameDto>> GetLegacyGamesAsync()
        {
            var query = new QueryDefinition("SELECT * FROM c WHERE c.type = 'BowlGame'");
            var iterator = _container.GetItemQueryIterator<dynamic>(query);
            var games = new List<LegacyGameDto>();

            while (iterator.HasMoreResults)
            {
                foreach (var item in await iterator.ReadNextAsync())
                {
                    games.Add(new LegacyGameDto
                    {
                        Id = item.id,
                        Description = $"{item.bowlName}: {item.teamHome} vs {item.teamAway}",
                        HomeTeam = item.teamHome,
                        AwayTeam = item.teamAway,
                        StartTime = item.startTime
                    });
                }
            }
            return games;
        }

        public async Task<List<string>> GetLegacyTeamNamesAsync()
        {
             // This logic was previously inside AnalyzeLegacyData. 
             // Implementing optimized query or processing if possible, but raw iteration is safer for legacy data structure variability.
             // Reusing the combined analysis method is better for performance (single pass).
             // Keeping this for interface completeness if needed separately.
             var (games, teamNames, count) = await AnalyzeLegacyDataAsync();
             return teamNames;
        }

        public async Task<(List<LegacyGameDto> Games, List<string> TeamNames, int EntryCount)> AnalyzeLegacyDataAsync()
        {
             var games = await GetLegacyGamesAsync();
             
             var teamNames = new HashSet<string>();
             int entryCount = 0;
             var query = new QueryDefinition("SELECT * FROM c WHERE c.type = 'BracketEntry'");
             var iterator = _container.GetItemQueryIterator<dynamic>(query);

             while (iterator.HasMoreResults)
             {
                 foreach (var item in await iterator.ReadNextAsync())
                 {
                     entryCount++;
                     if (item.picks != null)
                     {
                         try
                         {
                             var picksString = item.picks.ToString();
                             var picks = JsonConvert.DeserializeObject<Dictionary<string, string>>(picksString);
                             if (picks != null)
                             {
                                 foreach (var team in picks.Values)
                                 {
                                     if (!string.IsNullOrWhiteSpace(team))
                                     {
                                         teamNames.Add(team);
                                     }
                                 }
                             }
                         }
                         catch
                         {
                             // Ignore parsing errors
                         }
                     }
                 }
             }

             return (games, teamNames.OrderBy(t => t).ToList(), entryCount);
        }

        public async Task<List<dynamic>> GetLegacyEntriesAsync()
        {
            var query = new QueryDefinition("SELECT * FROM c WHERE c.type = 'BracketEntry'");
            var iterator = _container.GetItemQueryIterator<dynamic>(query);
            var entries = new List<dynamic>();

            while (iterator.HasMoreResults)
            {
                foreach (var item in await iterator.ReadNextAsync())
                {
                    entries.Add(item);
                }
            }
            return entries;
        }
    }
}
