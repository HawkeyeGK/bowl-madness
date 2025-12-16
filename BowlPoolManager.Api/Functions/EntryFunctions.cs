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
                // Security Check
                var authResult = await SecurityHelper.ValidateSuperAdminAsync(req, _cosmosService);
                if (!authResult.IsValid)
                {
                    _logger.LogWarning("SaveEntry blocked: Unauthorized or Forbidden.");
                    return authResult.ErrorResponse!;
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

        // NEW: Delete Endpoint
        [Function("DeleteEntry")]
        public async Task<HttpResponseData> DeleteEntry([HttpTrigger(AuthorizationLevel.Anonymous, "delete")] HttpRequestData req)
        {
            _logger.LogInformation("Deleting a bracket entry.");

            try
            {
                // Security Check (SuperAdmin Only)
                var authResult = await SecurityHelper.ValidateSuperAdminAsync(req, _cosmosService);
                if (!authResult.IsValid)
                {
                    return authResult.ErrorResponse!;
                }

                var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                var id = query["id"];

                if (string.IsNullOrEmpty(id))
                {
                    return req.CreateResponse(HttpStatusCode.BadRequest);
                }

                await _cosmosService.DeleteEntryAsync(id);

                return req.CreateResponse(HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DeleteEntry failed.");
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }
    }
}
