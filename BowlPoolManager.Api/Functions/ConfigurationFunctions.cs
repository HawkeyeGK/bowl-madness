using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Api.Repositories;
using BowlPoolManager.Api.Services;

namespace BowlPoolManager.Api.Functions
{
    public class ConfigurationFunctions
    {
        private readonly ILogger _logger;
        private readonly IConfigurationRepository _configRepo;
        private readonly IEspnDataService _espnService;

        public ConfigurationFunctions(ILoggerFactory loggerFactory, IConfigurationRepository configRepo, IEspnDataService espnService)
        {
            _logger = loggerFactory.CreateLogger<ConfigurationFunctions>();
            _configRepo = configRepo;
            _espnService = espnService;
        }

        [Function("GetTeamConfig")]
        public async Task<HttpResponseData> GetTeamConfig([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            _logger.LogInformation("Getting team configuration.");

            try
            {
                var config = await _configRepo.GetTeamConfigAsync();

                if (config == null)
                {
                    _logger.LogWarning("Team configuration document not found.");
                    return req.CreateResponse(HttpStatusCode.NotFound);
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(config);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetTeamConfig failed.");
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }

        [Function("GetBasketballTeamConfig")]
        public async Task<HttpResponseData> GetBasketballTeamConfig([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            _logger.LogInformation("Getting basketball team configuration.");

            try
            {
                var config = await _configRepo.GetBasketballTeamConfigAsync();

                if (config == null)
                {
                    _logger.LogWarning("Basketball team configuration document not found.");
                    return req.CreateResponse(HttpStatusCode.NotFound);
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(config);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetBasketballTeamConfig failed.");
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }

        [Function("GetEspnTeams")]
        public async Task<HttpResponseData> GetEspnTeams([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            _logger.LogInformation("Fetching ESPN team list.");

            try
            {
                var teams = await _espnService.GetTeamsAsync();

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(teams);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetEspnTeams failed.");
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }
    }
}
