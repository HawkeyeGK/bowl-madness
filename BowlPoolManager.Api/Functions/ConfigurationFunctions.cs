using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Api.Repositories;

namespace BowlPoolManager.Api.Functions
{
    public class ConfigurationFunctions
    {
        private readonly ILogger _logger;
        private readonly IConfigurationRepository _configRepo;

        public ConfigurationFunctions(ILoggerFactory loggerFactory, IConfigurationRepository configRepo)
        {
            _logger = loggerFactory.CreateLogger<ConfigurationFunctions>();
            _configRepo = configRepo;
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
    }
}
