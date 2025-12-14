using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using BowlPoolManager.Api.Services;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core; // Added for Constants
using System.Text.Json;
using System.Text; // Added for Encoding

namespace BowlPoolManager.Api.Functions
{
    public class PoolFunctions
    {
        private readonly ILogger _logger;
        private readonly ICosmosDbService _cosmosService;

        public PoolFunctions(ILoggerFactory loggerFactory, ICosmosDbService cosmosService)
        {
            _logger = loggerFactory.CreateLogger<PoolFunctions>();
            _cosmosService = cosmosService;
        }

        [Function("CreatePool")]
        public async Task<HttpResponseData> CreatePool([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            _logger.LogInformation("Creating a new pool.");

            try
            {
                // 1. Authenticate: Parse SWA Header
                var principal = ParseSwaHeader(req);
                if (principal == null || string.IsNullOrEmpty(principal.UserId))
                {
                    return req.CreateResponse(HttpStatusCode.Unauthorized);
                }

                // 2. Authorize: Check for SuperAdmin Role in DB
                var userProfile = await _cosmosService.GetUserAsync(principal.UserId);
                if (userProfile == null || userProfile.AppRole != Constants.Roles.SuperAdmin)
                {
                    _logger.LogWarning($"User {principal.UserId} attempted to create a pool but is not SuperAdmin.");
                    return req.CreateResponse(HttpStatusCode.Forbidden);
                }

                // 3. Deserialize & Validate
                var pool = await JsonSerializer.DeserializeAsync<BowlPool>(req.Body);
                if (pool == null)
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync("Invalid pool data.");
                    return badResponse;
                }

                if (string.IsNullOrWhiteSpace(pool.AccessKey))
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync("Access Key is required to secure the pool.");
                    return badResponse;
                }

                // 4. Save
                await _cosmosService.AddPoolAsync(pool);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(pool);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CreatePool failed.");
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }

        [Function("GetPools")]
        public async Task<HttpResponseData> GetPools([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            _logger.LogInformation("Getting all pools.");
            var pools = await _cosmosService.GetPoolsAsync();
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(pools);
            return response;
        }

        private ClientPrincipal? ParseSwaHeader(HttpRequestData req)
        {
            try
            {
                if (!req.Headers.TryGetValues("x-ms-client-principal", out var headerValues)) return null;
                var header = headerValues.FirstOrDefault();
                if (string.IsNullOrEmpty(header)) return null;

                var data = Convert.FromBase64String(header);
                var json = Encoding.UTF8.GetString(data);
                return JsonSerializer.Deserialize<ClientPrincipal>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                return null;
            }
        }
    }
}
