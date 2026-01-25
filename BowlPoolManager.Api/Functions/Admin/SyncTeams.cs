using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Cosmos;
using BowlPoolManager.Core;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Api.Services;

namespace BowlPoolManager.Api.Functions.Admin
{
    public class SyncTeams
    {
        private readonly ICfbdService _cfbdService;
        private readonly Container _container;
        private readonly ILogger<SyncTeams> _logger;

        public SyncTeams(ICfbdService cfbdService, CosmosClient cosmosClient, ILogger<SyncTeams> logger)
        {
            _cfbdService = cfbdService;
            _logger = logger;
            // Get reference to Configuration container
            _container = cosmosClient.GetContainer(Constants.Database.DbName, Constants.Database.ConfigurationContainer);
        }

        [Function("SyncTeams")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = "admin/sync-teams")] HttpRequest req)
        {
            _logger.LogInformation("Starting Team Sync...");

            // 1. Fetch Teams
            var teams = await _cfbdService.GetFbsTeamsAsync();
            if (teams == null || !teams.Any())
            {
                return new BadRequestObjectResult("No teams found from CFBD API.");
            }

            // 2. Create Config Object with Sorted Teams
            var config = new TeamConfig
            {
                Teams = teams.OrderBy(t => t.Conference).ThenBy(t => t.School).ToList(),
                LastUpdated = DateTime.UtcNow
            };

            // 3. Upsert to Cosmos
            try
            {
                await _container.UpsertItemAsync(config, new PartitionKey(config.Id));
                _logger.LogInformation($"Successfully synced {config.Teams.Count} teams.");
                
                return new OkObjectResult(new { 
                    Message = "Sync Successful", 
                    Count = config.Teams.Count, 
                    LastUpdated = config.LastUpdated 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upsert Team Config.");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    }
}
