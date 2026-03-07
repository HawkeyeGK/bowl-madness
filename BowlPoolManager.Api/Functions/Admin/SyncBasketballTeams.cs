using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
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
        private readonly IConfigurationRepository _configRepo;
        private readonly ILogger<SyncBasketballTeams> _logger;
        private readonly IUserRepository _userRepo;

        public SyncBasketballTeams(IBasketballDataService basketballService, IConfigurationRepository configRepo, ILogger<SyncBasketballTeams> logger, IUserRepository userRepo)
        {
            _basketballService = basketballService;
            _configRepo = configRepo;
            _logger = logger;
            _userRepo = userRepo;
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

            // FBS auto-match: copy logos from matching FBS teams
            var fbsConfig = await _configRepo.GetTeamConfigAsync();
            if (fbsConfig?.Teams != null)
            {
                var fbsLookup = fbsConfig.Teams
                    .Where(t => t.Logos != null && t.Logos.Any())
                    .ToDictionary(t => t.School.Trim(), t => t, StringComparer.OrdinalIgnoreCase);

                int matched = 0;
                foreach (var team in teams)
                {
                    if (fbsLookup.TryGetValue(team.School.Trim(), out var fbsTeam))
                    {
                        team.Logos = fbsTeam.Logos;
                        matched++;
                    }
                }
                _logger.LogInformation("FBS auto-match enriched {Count} basketball teams with logos.", matched);
            }

            var config = new TeamConfig
            {
                Id = Constants.ConfigDocumentIds.BasketballTeamConfig,
                Teams = teams.OrderBy(t => t.Conference).ThenBy(t => t.School).ToList(),
                LastUpdated = DateTime.UtcNow
            };

            try
            {
                await _configRepo.SaveBasketballTeamConfigAsync(config);
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
