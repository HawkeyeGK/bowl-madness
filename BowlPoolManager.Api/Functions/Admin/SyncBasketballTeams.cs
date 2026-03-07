using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Cosmos;
using BowlPoolManager.Core;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Api.Services;
using BowlPoolManager.Api.Repositories;
using BowlPoolManager.Api.Helpers;

namespace BowlPoolManager.Api.Functions.Admin
{
    public class SyncBasketballTeams
    {
        private readonly IBasketballDataService _basketballService;
        private readonly Container _container;
        private readonly ILogger<SyncBasketballTeams> _logger;
        private readonly IUserRepository _userRepo;

        public SyncBasketballTeams(IBasketballDataService basketballService, CosmosClient cosmosClient, ILogger<SyncBasketballTeams> logger, IUserRepository userRepo)
        {
            _basketballService = basketballService;
            _logger = logger;
            _userRepo = userRepo;
            _container = cosmosClient.GetContainer(Constants.Database.DbName, Constants.Database.ConfigurationContainer);
        }

        [Function("SyncBasketballTeams")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            var authResult = await SecurityHelper.ValidateSuperAdminAsync(req, _userRepo);
            if (!authResult.IsValid) return authResult.ErrorResponse!;

            _logger.LogInformation("Starting Basketball Team Sync...");

            var teams = await _basketballService.GetTeamsAsync();
            if (teams == null || !teams.Any())
            {
                var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                await badReq.WriteStringAsync("No teams found from Basketball API.");
                return badReq;
            }

            var config = new TeamConfig
            {
                Id = Constants.ConfigDocumentIds.BasketballTeamConfig,
                Teams = teams.OrderBy(t => t.Conference).ThenBy(t => t.School).ToList(),
                LastUpdated = DateTime.UtcNow
            };

            try
            {
                await _container.UpsertItemAsync(config, new PartitionKey(config.Id));
                _logger.LogInformation("Successfully synced {Count} basketball teams.", config.Teams.Count);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    Message = "Sync Successful",
                    Count = config.Teams.Count,
                    LastUpdated = config.LastUpdated
                });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upsert Basketball Team Config.");
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }
    }
}
