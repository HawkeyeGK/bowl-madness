using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using BowlPoolManager.Core; // Ensure access to Constants

namespace BowlPoolManager.Api.Functions
{
    public class BackupFunctions
    {
        private readonly CosmosClient _cosmosClient;
        private readonly ILogger<BackupFunctions> _logger;

        public BackupFunctions(CosmosClient cosmosClient, ILogger<BackupFunctions> logger)
        {
            _cosmosClient = cosmosClient;
            _logger = logger;
        }

        [Function("GetBackupData")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
        {
            _logger.LogInformation("Backup download requested.");

            var db = _cosmosClient.GetDatabase(Constants.Database.DbName);
            var seasonsContainer = db.GetContainer(Constants.Database.SeasonsContainer);
            var picksContainer = db.GetContainer(Constants.Database.PicksContainer);

            // 1. Fetch Bowl Games (from Seasons Container)
            // Note: We scan all seasons or just current? For a full backup, usually all valid games.
            // Using a cross-partition query to get all definitions.
            var gamesQuery = new QueryDefinition("SELECT c.id, c.bowlName FROM c WHERE c.type = 'BowlGame'");
            var gameMap = new Dictionary<string, string>();
            
            using (var feed = seasonsContainer.GetItemQueryIterator<BowlGameDto>(gamesQuery))
            {
                while (feed.HasMoreResults)
                {
                    var response = await feed.ReadNextAsync();
                    foreach (var game in response)
                    {
                        if (!gameMap.ContainsKey(game.id))
                        {
                            gameMap.Add(game.id, game.bowlName);
                        }
                    }
                }
            }

            // 2. Fetch Picks (from Picks Container)
            var picksQuery = new QueryDefinition("SELECT * FROM c WHERE c.type = 'BracketEntry'");
            var rawPicks = new List<BracketEntryDto>();

            using (var feed = picksContainer.GetItemQueryIterator<BracketEntryDto>(picksQuery))
            {
                while (feed.HasMoreResults)
                {
                    var response = await feed.ReadNextAsync();
                    rawPicks.AddRange(response);
                }
            }

            // 3. Enrich Data (Join Picks with Bowl Names)
            var exportData = rawPicks.Select(entry => new 
            {
                PlayerName = entry.playerName,
                PlayerId = entry.userId,
                Timestamp = entry.createdOn,
                TieBreaker = entry.tieBreakerPoints,
                Picks = entry.picks?.Select(p => new 
                {
                    BowlName = gameMap.ContainsKey(p.Key) ? gameMap[p.Key] : "Unknown Bowl",
                    GameId = p.Key,
                    SelectedTeam = p.Value
                }).OrderBy(x => x.BowlName).ToList()
            }).OrderBy(x => x.PlayerName).ToList();

            // 4. Serialize and Return
            var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
            var bytes = Encoding.UTF8.GetBytes(json);
            var fileName = $"BowlPool_Backup_{DateTime.UtcNow:yyyyMMdd}.json";

            return new FileContentResult(bytes, "application/json")
            {
                FileDownloadName = fileName
            };
        }

        // DTOs (Keep existing)
        private class BowlGameDto
        {
            public string id { get; set; } = string.Empty;
            public string bowlName { get; set; } = string.Empty;
        }

        private class BracketEntryDto
        {
            public string userId { get; set; } = string.Empty;
            public string playerName { get; set; } = string.Empty;
            public Dictionary<string, string> picks { get; set; } = new();
            public int tieBreakerPoints { get; set; }
            public DateTime createdOn { get; set; }
        }
    }
}
