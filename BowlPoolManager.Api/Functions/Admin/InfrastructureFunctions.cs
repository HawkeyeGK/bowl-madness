using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using BowlPoolManager.Core;

namespace BowlPoolManager.Api.Functions.Admin
{
    public class InfrastructureFunctions
    {
        private readonly CosmosClient _cosmosClient;
        private readonly ILogger<InfrastructureFunctions> _logger;

        public InfrastructureFunctions(CosmosClient cosmosClient, ILogger<InfrastructureFunctions> logger)
        {
            _cosmosClient = cosmosClient;
            _logger = logger;
        }

        [Function("InitializeDatabase")]
        public async Task<IActionResult> InitializeDatabase(
            [HttpTrigger(AuthorizationLevel.Admin, "post", Route = "Admin/Initialize")] HttpRequest req)
        {
            _logger.LogInformation("Initializing Database Infrastructure...");

            try
            {
                // Create Database
                var dbResponse = await _cosmosClient.CreateDatabaseIfNotExistsAsync(Constants.Database.DbName);
                var db = dbResponse.Database;

                // Create Containers
                await db.CreateContainerIfNotExistsAsync(Constants.Database.PlayersContainer, Constants.Database.DefaultPartitionKey);
                await db.CreateContainerIfNotExistsAsync(Constants.Database.SeasonsContainer, Constants.Database.SeasonPartitionKey);
                await db.CreateContainerIfNotExistsAsync(Constants.Database.PicksContainer, Constants.Database.SeasonPartitionKey);
                await db.CreateContainerIfNotExistsAsync(Constants.Database.ArchivesContainer, Constants.Database.SeasonPartitionKey);
                await db.CreateContainerIfNotExistsAsync(Constants.Database.ConfigurationContainer, Constants.Database.DefaultPartitionKey);

                _logger.LogInformation("Database infrastructure initialized successfully.");
                return new OkObjectResult("Database infrastructure initialized successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing database infrastructure.");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    }
}
