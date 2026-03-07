using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using BowlPoolManager.Api.Services;
using BowlPoolManager.Api.Repositories;
using BowlPoolManager.Api.Helpers;

namespace BowlPoolManager.Api.Functions.Admin
{
    public class SyncEspnLogos
    {
        private readonly IEspnDataService _espnService;
        private readonly IConfigurationRepository _configRepo;
        private readonly ILogger<SyncEspnLogos> _logger;
        private readonly IUserRepository _userRepo;

        public SyncEspnLogos(IEspnDataService espnService, IConfigurationRepository configRepo, ILogger<SyncEspnLogos> logger, IUserRepository userRepo)
        {
            _espnService = espnService;
            _configRepo = configRepo;
            _logger = logger;
            _userRepo = userRepo;
        }

        [Function("SyncEspnLogos")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            var authResult = await SecurityHelper.ValidateSuperAdminAsync(req, _userRepo);
            if (!authResult.IsValid) return authResult.ErrorResponse!;

            _logger.LogInformation("Starting ESPN Logo Sync...");

            var basketballConfig = await _configRepo.GetBasketballTeamConfigAsync();
            if (basketballConfig == null || !basketballConfig.Teams.Any())
            {
                var notFound = req.CreateResponse(HttpStatusCode.BadRequest);
                await notFound.WriteStringAsync("No basketball teams found. Run Basketball Team Sync first.");
                return notFound;
            }

            var espnTeams = await _espnService.GetTeamsAsync();
            if (!espnTeams.Any())
            {
                var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                await badReq.WriteStringAsync("No teams returned from ESPN API.");
                return badReq;
            }

            var espnLookup = espnTeams
                .Where(t => t.Logos != null && t.Logos.Any())
                .ToDictionary(t => t.School.Trim(), t => t, StringComparer.OrdinalIgnoreCase);

            int enriched = 0;
            foreach (var team in basketballConfig.Teams)
            {
                // Only fill in logos that are still missing
                if (team.Logos != null && team.Logos.Any()) continue;

                if (espnLookup.TryGetValue(team.School.Trim(), out var espnTeam))
                {
                    team.Logos = espnTeam.Logos;
                    enriched++;
                }
            }

            basketballConfig.LastUpdated = DateTime.UtcNow;

            try
            {
                await _configRepo.SaveBasketballTeamConfigAsync(basketballConfig);
                _logger.LogInformation("ESPN Logo Sync enriched {Count} basketball teams.", enriched);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    Message = "ESPN Logo Sync Successful",
                    EnrichedCount = enriched,
                    TotalTeams = basketballConfig.Teams.Count,
                    LastUpdated = basketballConfig.LastUpdated
                });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save basketball config after ESPN logo sync.");
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }
    }
}
