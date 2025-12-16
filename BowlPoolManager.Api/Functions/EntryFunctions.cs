using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using BowlPoolManager.Api.Services;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core;
using System.Text.Json;
using BowlPoolManager.Api.Helpers;

namespace BowlPoolManager.Api.Functions
{
    public class EntryFunctions
    {
        private readonly ILogger _logger;
        private readonly ICosmosDbService _cosmosService;

        public EntryFunctions(ILoggerFactory loggerFactory, ICosmosDbService cosmosService)
        {
            _logger = loggerFactory.CreateLogger<EntryFunctions>();
            _cosmosService = cosmosService;
        }

        [Function("GetEntries")]
        public async Task<HttpResponseData> GetEntries([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            _logger.LogInformation("Getting all bracket entries.");
            var entries = await _cosmosService.GetEntriesAsync();
            
            // Optional: Sort by Player Name
            var sortedEntries = entries.OrderBy(e => e.PlayerName).ToList();

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(sortedEntries);
            return response;
        }


        // NEW: Get Single Entry
        [Function("GetEntry")]
        public async Task<HttpResponseData> GetEntry([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var id = query["id"];

            if (string.IsNullOrEmpty(id))
            {
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            _logger.LogInformation($"Getting entry {id}");
            var entry = await _cosmosService.GetEntryAsync(id);

            if (entry == null)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(entry);
            return response;
        }

        [Function("SaveEntry")]
        public async Task<HttpResponseData> SaveEntry([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            _logger.LogInformation("Saving a bracket entry.");

            try
            {
                // 1. Authenticate
                var principal = SecurityHelper.ParseSwaHeader(req);
                if (principal == null || string.IsNullOrEmpty(principal.UserId))
                {
                    return req.CreateResponse(HttpStatusCode.Unauthorized);
                }

                // 2. Authorize (SuperAdmin Only)
                var userProfile = await _cosmosService.GetUserAsync(principal.UserId);
                if (userProfile == null || userProfile.AppRole != Constants.Roles.SuperAdmin)
                {
                    _logger.LogWarning($"User {principal.UserId} attempted to save an entry without SuperAdmin rights.");
                    return req.CreateResponse(HttpStatusCode.Forbidden);
                }

                // 3. Process
                var entry = await JsonSerializer.DeserializeAsync<BracketEntry>(req.Body);
                if (entry == null || string.IsNullOrWhiteSpace(entry.PlayerName))
                {
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteStringAsync("Invalid entry data. Player Name is required.");
                    return badReq;
                }

                // 4. Save
                await _cosmosService.AddEntryAsync(entry);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(entry);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SaveEntry failed.");
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }
    }
}
