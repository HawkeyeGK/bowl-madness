using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using BowlPoolManager.Core.Dtos;
using BowlPoolManager.Api.Repositories;
using BowlPoolManager.Api.Helpers;

namespace BowlPoolManager.Api.Functions.Admin
{
    public class ApplyLogoMappings
    {
        private readonly IConfigurationRepository _configRepo;
        private readonly ILogger<ApplyLogoMappings> _logger;
        private readonly IUserRepository _userRepo;

        public ApplyLogoMappings(IConfigurationRepository configRepo, ILogger<ApplyLogoMappings> logger, IUserRepository userRepo)
        {
            _configRepo = configRepo;
            _logger = logger;
            _userRepo = userRepo;
        }

        [Function("ApplyLogoMappings")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            var authResult = await SecurityHelper.ValidateSuperAdminAsync(req, _userRepo);
            if (!authResult.IsValid) return authResult.ErrorResponse!;

            var body = await req.ReadAsStringAsync();
            List<LogoMappingDto>? mappings;
            try
            {
                mappings = JsonConvert.DeserializeObject<List<LogoMappingDto>>(body ?? "[]");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Invalid request body for ApplyLogoMappings.");
                var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                await badReq.WriteStringAsync("Invalid JSON body.");
                return badReq;
            }

            if (mappings == null || !mappings.Any())
            {
                var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                await badReq.WriteStringAsync("No mappings provided.");
                return badReq;
            }

            var config = await _configRepo.GetBasketballTeamConfigAsync();
            if (config == null)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteStringAsync("Basketball team config not found.");
                return notFound;
            }

            var mappingLookup = mappings.ToDictionary(m => m.SchoolId, m => m.LogoUrl);
            int applied = 0;
            foreach (var team in config.Teams)
            {
                if (mappingLookup.TryGetValue(team.SchoolId, out var logoUrl) && !string.IsNullOrEmpty(logoUrl))
                {
                    team.Logos = new List<string> { logoUrl };
                    applied++;
                }
            }

            config.LastUpdated = DateTime.UtcNow;

            try
            {
                await _configRepo.SaveBasketballTeamConfigAsync(config);
                _logger.LogInformation("Applied {Count} manual logo mappings.", applied);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { Message = "Mappings Applied", AppliedCount = applied });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save basketball config after applying logo mappings.");
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }
    }
}
