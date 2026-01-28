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
                        StartTime = item.startTime,
                        SeasonId = !string.IsNullOrEmpty((string?)item.seasonId) ? (string?)item.seasonId : "LEGACY"
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
             var (games, teamNames, seasonIds, pools, count, _) = await AnalyzeLegacyDataAsync();
             return teamNames;
        }

        public async Task<(List<LegacyGameDto> Games, List<string> TeamNames, List<string> SeasonIds, List<LegacyPoolDto> Pools, int EntryCount, string DebugInfo)> AnalyzeLegacyDataAsync()
        {
             var games = await GetLegacyGamesAsync();
             
             var teamNames = new HashSet<string>();
             var seasonIds = new HashSet<string>();
             var pools = new List<LegacyPoolDto>(); 
             var seenPools = new HashSet<string>(); 

             int entryCount = 0;
             string debugInfo = "No entries found";

             var query = new QueryDefinition("SELECT * FROM c WHERE c.type = 'BracketEntry'");
             var iterator = _container.GetItemQueryIterator<dynamic>(query);

             while (iterator.HasMoreResults)
             {
                 foreach (var item in await iterator.ReadNextAsync())
                 {
                     if (entryCount == 0)
                     {
                         try { debugInfo = JsonConvert.SerializeObject(item); } catch { debugInfo = "Error serializing item"; }
                     }
                     entryCount++;
                     
                     string? sId = null;
                     string? pId = null;

                     // Try multiple casings/types for Season
                     try 
                     { 
                        if (item.seasonId != null) sId = item.seasonId.ToString();
                        else if (item.SeasonId != null) sId = item.SeasonId.ToString();
                        else if (item.season != null) sId = item.season.ToString();
                        else if (item.Season != null) sId = item.Season.ToString();
                     } catch {}

                     try 
                     { 
                        if (item.poolId != null) pId = item.poolId.ToString();
                        else if (item.PoolId != null) pId = item.PoolId.ToString();
                     } catch {}

                     // If no season found on the item, assume it belongs to the single "LEGACY" season
                     if (string.IsNullOrEmpty(sId))
                     {
                         sId = "LEGACY";
                     }

                     // Capture explicit 'season' or other descriptive properties for display name
                     string poolName = "";
                     try 
                     {
                         if (item.season != null) poolName = item.season.ToString();
                         else if (item.Season != null) poolName = item.Season.ToString();
                         else if (item.year != null) poolName = item.year.ToString();
                         else if (item.Year != null) poolName = item.Year.ToString();
                         else if (item.description != null) poolName = item.description.ToString();
                         else if (item.poolName != null) poolName = item.poolName.ToString();
                     } catch {}

                     if (!string.IsNullOrEmpty(sId))
                     {
                         seasonIds.Add(sId);

                         // Capture Pool if present
                         if (!string.IsNullOrEmpty(pId))
                         {
                             var key = $"{sId}|{pId}";
                             if (!seenPools.Contains(key))
                             {
                                 string finalPoolName = pId; // Default to ID
                                 if (!string.IsNullOrWhiteSpace(poolName)) 
                                 {
                                     finalPoolName = poolName;
                                 }
                                 else if (sId != "LEGACY")
                                 {
                                     // If we found a real season ID/Name (e.g. "2024"), use that as the Pool Name 
                                     // since legacy often conflates them.
                                     finalPoolName = sId;
                                 }

                                 seenPools.Add(key);
                                 pools.Add(new LegacyPoolDto 
                                 { 
                                     SeasonId = sId, 
                                     PoolId = pId,
                                     PoolName = finalPoolName
                                 });
                             }
                         }
                     }

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
                         catch { }
                     }
                 }
             }

             return (games, teamNames.OrderBy(t => t).ToList(), seasonIds.OrderBy(s => s).ToList(), pools.OrderBy(p => p.SeasonId).ThenBy(p => p.PoolId).ToList(), entryCount, debugInfo);
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
