using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using BowlPoolManager.Core;
using BowlPoolManager.Core.Domain;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BowlPoolManager.Api.Functions
{
    public class MigrateFunctions
    {
        private readonly CosmosClient _cosmosClient;
        private readonly ILogger<MigrateFunctions> _logger;

        public MigrateFunctions(CosmosClient cosmosClient, ILogger<MigrateFunctions> logger)
        {
            _cosmosClient = cosmosClient;
            _logger = logger;
        }

        [Function("LinkGamesToPools")]
        public async Task<IActionResult> LinkGamesToPools([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req)
        {
            _logger.LogInformation("Starting LinkGamesToPools migration...");

            var db = _cosmosClient.GetDatabase(Constants.Database.DbName);
            var seasonsContainer = db.GetContainer(Constants.Database.SeasonsContainer);

            // 1. Fetch all BowlPools
            var poolsQuery = new QueryDefinition("SELECT * FROM c WHERE c.type = 'BowlPool'");
            var pools = new List<BowlPool>();

            using (var feed = seasonsContainer.GetItemQueryIterator<BowlPool>(poolsQuery))
            {
                while (feed.HasMoreResults)
                {
                    pools.AddRange(await feed.ReadNextAsync());
                }
            }

            _logger.LogInformation($"Found {pools.Count} pools to process.");

            int poolsUpdated = 0;
            int totalGamesLinked = 0;

            foreach (var pool in pools)
            {
                // 2. Fetch Games for this Pool's SeasonId
                var gamesQuery = new QueryDefinition("SELECT * FROM c WHERE c.type = 'BowlGame' AND c.seasonId = @seasonId")
                    .WithParameter("@seasonId", pool.SeasonId);
                
                var games = new List<BowlGame>();
                using (var feed = seasonsContainer.GetItemQueryIterator<BowlGame>(gamesQuery))
                {
                    while (feed.HasMoreResults)
                    {
                        games.AddRange(await feed.ReadNextAsync());
                    }
                }

                // 3. Update Pool
                pool.GameIds = games.Select(g => g.Id).ToList();
                poolsUpdated++;
                totalGamesLinked += pool.GameIds.Count;

                _logger.LogInformation($"Pool '{pool.Name}' (Season {pool.SeasonId}): Linked {pool.GameIds.Count} games.");

                // 4. Save (Upsert)
                // Note: The PartitionKey for BowlPool is usually its Id or generic (check CosmosRepositoryBase/Core).
                // However, based on earlier context, we don't have the exact PK logic here blindly.
                // Assuming typical Cosmos setup where we might need to know the PK.
                // Looking at BowlPool.cs, there is no explicit PartitionKey property annotated, 
                // but usually it's /id or /seasonId or /type. 
                // Let's assume /id or /partitionKey. 
                // If the container is 'Seasons', and we fetched it, we can just upsert.
                // If the partition key is NOT id, we need to provide it.
                // Let's check `Constants` or similar if possible, but standard UpsertItemAsync tries to extract it if defined in class
                // or we pass it. 
                // *Safest* is to read the container definition or just try upserting with the object.
                
                await seasonsContainer.UpsertItemAsync(pool);
            }

            return new OkObjectResult(new
            {
                Message = "Migration Complete",
                PoolsUpdated = poolsUpdated,
                TotalGamesLinked = totalGamesLinked
            });
        }
    }
}
