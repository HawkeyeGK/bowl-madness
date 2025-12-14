using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using BowlPoolManager.Core.Dtos;

namespace BowlPoolManager.Api.Functions
{
    public class HealthCheckFunction
    {
        private readonly ILogger<HealthCheckFunction> _logger;

        public HealthCheckFunction(ILogger<HealthCheckFunction> logger)
        {
            _logger = logger;
        }

        [Function("HealthCheck")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "healthcheck")] HttpRequestData req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a HealthCheck request.");
            
            var responseDto = new HealthCheckResponseDto
            {
                Status = "OK",
                Source = "Azure Function API (Isolated Worker)",
                Message = "API is running and accessible.",
                Timestamp = DateTime.UtcNow
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(responseDto);
            return response;
        }
    }
}
