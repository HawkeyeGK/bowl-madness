using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Cosmos;
using BowlPoolManager.Core;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Dtos;
using BowlPoolManager.Api.Repositories;
using BowlPoolManager.Api.Helpers;

namespace BowlPoolManager.Api.Functions.Admin
{
    public class AdminDashboardFunctions
    {
        private readonly Container _container;
        private readonly ILogger<AdminDashboardFunctions> _logger;
        private readonly IUserRepository _userRepo;

        public AdminDashboardFunctions(CosmosClient cosmosClient, ILogger<AdminDashboardFunctions> logger, IUserRepository userRepo)
        {
            _logger = logger;
            _userRepo = userRepo;
            _container = cosmosClient.GetContainer(Constants.Database.DbName, Constants.Database.ConfigurationContainer);
        }

        [Function("GetIntegrationStatus")]
        public async Task<HttpResponseData> GetIntegrationStatus([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            var authResult = await SecurityHelper.ValidateSuperAdminAsync(req, _userRepo);
            if (!authResult.IsValid) return authResult.ErrorResponse!;

            _logger.LogInformation("Getting Integration Status...");

            var status = new IntegrationStatusDto
            {
                IsApiKeyConfigured = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CfbdApiKey"))
            };

            var key = Environment.GetEnvironmentVariable("CfbdApiKey");
            var keyStatus = string.IsNullOrEmpty(key) ? "NULL/EMPTY" : $"Present (Len: {key.Length})";
            var allKeys = string.Join(", ", Environment.GetEnvironmentVariables().Keys.Cast<string>().Where(k => k.StartsWith("CFBD", StringComparison.OrdinalIgnoreCase) || k.Contains("Key", StringComparison.OrdinalIgnoreCase)));

            status.DebugDetails = $"Target Var: 'CfbdApiKey'. Status: {keyStatus}. \nRelevant Env Vars Found: [{allKeys}]";

            _logger.LogInformation($"[Diagnose] {status.DebugDetails}");

            try
            {
                var cosResponse = await _container.ReadItemAsync<TeamConfig>(Constants.ConfigDocumentIds.FbsTeamConfig, new PartitionKey(Constants.ConfigDocumentIds.FbsTeamConfig));
                var config = cosResponse.Resource;
                status.LastSyncUtc = config.LastUpdated;
                status.TeamCount = config.Teams.Count;
                status.Message = "Integration is healthy.";
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                status.Message = "Team configuration not found. Please sync teams.";
                status.TeamCount = 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving football team configuration.");
                status.Message = "Error checking database status.";
            }

            status.IsBasketballApiKeyConfigured = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CfbdApiKeyHoops"));

            try
            {
                var bbResponse = await _container.ReadItemAsync<TeamConfig>(Constants.ConfigDocumentIds.BasketballTeamConfig, new PartitionKey(Constants.ConfigDocumentIds.BasketballTeamConfig));
                var bbConfig = bbResponse.Resource;
                status.BasketballLastSyncUtc = bbConfig.LastUpdated;
                status.BasketballTeamCount = bbConfig.Teams.Count;
                status.BasketballMessage = "Integration is healthy.";
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                status.BasketballMessage = "Basketball team configuration not found. Please sync teams.";
                status.BasketballTeamCount = 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving basketball team configuration.");
                status.BasketballMessage = "Error checking database status.";
            }

            var httpResponse = req.CreateResponse(HttpStatusCode.OK);
            await httpResponse.WriteAsJsonAsync(status);
            return httpResponse;
        }
    }
}
