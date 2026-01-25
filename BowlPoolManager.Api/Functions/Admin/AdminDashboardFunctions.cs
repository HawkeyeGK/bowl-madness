using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Cosmos;
using BowlPoolManager.Core;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Dtos;

namespace BowlPoolManager.Api.Functions.Admin
{
    public class AdminDashboardFunctions
    {
        private readonly Container _container;
        private readonly ILogger<AdminDashboardFunctions> _logger;

        public AdminDashboardFunctions(CosmosClient cosmosClient, ILogger<AdminDashboardFunctions> logger)
        {
            _logger = logger;
            _container = cosmosClient.GetContainer(Constants.Database.DbName, Constants.Database.ConfigurationContainer);
        }

        [Function("GetIntegrationStatus")]
        public async Task<IActionResult> GetIntegrationStatus([HttpTrigger(AuthorizationLevel.Function, "get", Route = "admin/integration-status")] HttpRequest req)
        {
            _logger.LogInformation("Getting Integration Status...");

            var status = new IntegrationStatusDto
            {
                IsApiKeyConfigured = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CFBD_API_KEY"))
            };

            try
            {
                var response = await _container.ReadItemAsync<TeamConfig>("Config_Teams_FBS", new PartitionKey("Config_Teams_FBS"));
                var config = response.Resource;
                
                status.LastSyncUtc = config.LastUpdated;
                status.TeamCount = config.Teams.Count;
                status.Message = "Integration is healthy.";
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                status.Message = "Team configuration not found. Please sync teams.";
                status.TeamCount = 0;
            }
            catch (Exception ex)
            {
                 _logger.LogError(ex, "Error retrieving team configuration.");
                 status.Message = "Error checking database status.";
            }

            return new OkObjectResult(status);
        }
    }
}
