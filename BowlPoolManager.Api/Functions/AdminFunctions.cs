using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using BowlPoolManager.Api.Services;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core;
using BowlPoolManager.Api.Helpers;

namespace BowlPoolManager.Api.Functions
{
    public class AdminFunctions
    {
        private readonly ILogger _logger;
        private readonly ICosmosDbService _cosmosService;

        public AdminFunctions(ILoggerFactory loggerFactory, ICosmosDbService cosmosService)
        {
            _logger = loggerFactory.CreateLogger<AdminFunctions>();
            _cosmosService = cosmosService;
        }

        [Function("AdminGetUsers")]
        public async Task<HttpResponseData> AdminGetUsers([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            _logger.LogInformation("SuperAdmin accessing User List.");

            var auth = await SecurityHelper.ValidateSuperAdminAsync(req, _cosmosService);
            if (!auth.IsValid) return auth.ErrorResponse!;

            var users = await _cosmosService.GetUsersAsync();
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(users);
            return response;
        }

        [Function("AdminGetEntries")]
        public async Task<HttpResponseData> AdminGetEntries([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            _logger.LogInformation("SuperAdmin accessing Entry List (Raw).");

            var auth = await SecurityHelper.ValidateSuperAdminAsync(req, _cosmosService);
            if (!auth.IsValid) return auth.ErrorResponse!;

            // Optional: Support pool filtering if the Admin UI sends it
            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            string? poolIdFilter = query["poolId"];

            // Fetch RAW entries. No redaction. No lock checks.
            var entries = await _cosmosService.GetEntriesAsync(poolIdFilter);
            
            var sortedEntries = entries.OrderByDescending(e => e.CreatedOn).ToList();

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(sortedEntries);
            return response;
        }
    }
}
