using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using BowlPoolManager.Core;
using BowlPoolManager.Core.Domain;
using Newtonsoft.Json.Linq; 

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

        [Function("MigrateData")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req)
        {
            _logger.LogInformation("Starting Data Migration...");

            var db = _cosmosClient.GetDatabase(Constants.Database.DbName);
            var sourceContainer = db.GetContainer(Constants.Database.MainContainerName);
            
            var playersContainer = db.GetContainer(Constants.Database.PlayersContainer);
            var seasonsContainer = db.GetContainer(Constants.Database.SeasonsContainer);
            var picksContainer = db.GetContainer(Constants.Database.PicksContainer);

            int migratedPlayers = 0;
            int migratedGames = 0;
            int migratedPicks = 0;
            int migratedPools = 0;
            int errors = 0;

            // 1. Read All Documents from MainContainer
            // using JObject first to inspect 'type' safely
            var query = new QueryDefinition("SELECT * FROM c");
            using var iterator = sourceContainer.GetItemQueryIterator<JObject>(query);

            while (iterator.HasMoreResults)
            {
                var batch = await iterator.ReadNextAsync();
                foreach (var doc in batch)
                {
                    try
                    {
                        string type = doc["type"]?.ToString() ?? "Unknown";

                        switch (type)
                        {
                            case Constants.DocumentTypes.UserProfile:
                                var user = doc.ToObject<UserProfile>();
                                if (user != null)
                                {
                                    // Partition: /id
                                    await playersContainer.UpsertItemAsync(user, new PartitionKey(user.Id));
                                    migratedPlayers++;
                                }
                                break;

                            case Constants.DocumentTypes.BowlGame:
                                var game = doc.ToObject<BowlGame>();
                                if (game != null)
                                {
                                    // Inject SeasonId
                                    game.SeasonId = Constants.CurrentSeason; 
                                    // Partition: /seasonId
                                    await seasonsContainer.UpsertItemAsync(game, new PartitionKey(game.SeasonId));
                                    migratedGames++;
                                }
                                break;
                            
                            case Constants.DocumentTypes.BowlPool:
                                var pool = doc.ToObject<BowlPool>();
                                if (pool != null)
                                {
                                    pool.SeasonId = Constants.CurrentSeason;
                                    await seasonsContainer.UpsertItemAsync(pool, new PartitionKey(pool.SeasonId));
                                    migratedPools++;
                                }
                                break;

                            case Constants.DocumentTypes.BracketEntry:
                                var pick = doc.ToObject<BracketEntry>();
                                if (pick != null)
                                {
                                    pick.SeasonId = Constants.CurrentSeason;
                                    // Partition: /seasonId
                                    await picksContainer.UpsertItemAsync(pick, new PartitionKey(pick.SeasonId));
                                    migratedPicks++;
                                }
                                break;

                            default:
                                _logger.LogWarning($"Skipping document with unknown type: {type} ID: {doc["id"]}");
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to migrate document ID: {doc["id"]}");
                        errors++;
                    }
                }
            }

            var result = new 
            {
                Status = "Complete",
                Stats = new 
                {
                    Players = migratedPlayers,
                    Games = migratedGames,
                    Pools = migratedPools,
                    Picks = migratedPicks,
                    Errors = errors
                }
            };

            return new OkObjectResult(result);
        }
    }
}
