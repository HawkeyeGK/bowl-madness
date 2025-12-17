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
            _logger.LogInformation("Getting bracket entries.");
            
            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            string? poolId = query["poolId"];

            // 1. Fetch Entries (Optionally filtered by Pool)
            var entries = await _cosmosService.GetEntriesAsync(poolId);
            
            // 2. Redaction Logic (The Silencer)
            // If a PoolId is provided, we must respect its Lock Date.
            if (!string.IsNullOrEmpty(poolId))
            {
                var pool = await _cosmosService.GetPoolAsync(poolId);
                if (pool != null)
                {
                    // Check if the pool is still "Open" (Pre-Deadline)
                    bool isPoolOpen = DateTime.UtcNow < pool.LockDate;

                    if (isPoolOpen)
                    {
                        // Identify the requester
                        var principal = SecurityHelper.ParseSwaHeader(req);
                        string currentUserId = principal?.UserId ?? string.Empty;

                        // Redact picks for everyone EXCEPT the requester
                        foreach (var entry in entries)
                        {
                            if (entry.UserId != currentUserId)
                            {
                                entry.Picks = null; // Hide the data
                                entry.TieBreakerPoints = 0; // Hide the strategy
                            }
                        }
                    }
                }
            }

            var sortedEntries = entries.OrderBy(e => e.PlayerName).ToList();
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(sortedEntries);
            return response;
        }

        [Function("GetEntry")]
        public async Task<HttpResponseData> GetEntry([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var id = query["id"];

            if (string.IsNullOrEmpty(id)) return req.CreateResponse(HttpStatusCode.BadRequest);

            var entry = await _cosmosService.GetEntryAsync(id);
            if (entry == null) return req.CreateResponse(HttpStatusCode.NotFound);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(entry);
            return response;
        }

        // NEW: Get My Entry (Secure)
        [Function("GetMyEntry")]
        public async Task<HttpResponseData> GetMyEntry([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            var principal = SecurityHelper.ParseSwaHeader(req);
            if (principal == null || string.IsNullOrEmpty(principal.UserId))
            {
                return req.CreateResponse(HttpStatusCode.Unauthorized);
            }

            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var poolId = query["poolId"];

            if (string.IsNullOrEmpty(poolId)) return req.CreateResponse(HttpStatusCode.BadRequest);

            var entry = await _cosmosService.GetEntryByUserAsync(principal.UserId, poolId);

            // It is valid to return null (204 or 404) if I haven't joined yet
            if (entry == null) return req.CreateResponse(HttpStatusCode.NotFound);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(entry);
            return response;
        }

        // NEW: Save My Entry (The Gatekeeper)
        [Function("SaveMyEntry")]
        public async Task<HttpResponseData> SaveMyEntry([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            _logger.LogInformation("User attempting to save entry.");

            try
            {
                // 1. Identity Check
                var principal = SecurityHelper.ParseSwaHeader(req);
                if (principal == null || string.IsNullOrEmpty(principal.UserId))
                {
                    return req.CreateResponse(HttpStatusCode.Unauthorized);
                }

                var entry = await JsonSerializer.DeserializeAsync<BracketEntry>(req.Body);
                if (entry == null) return req.CreateResponse(HttpStatusCode.BadRequest);

                // Enforce Ownership
                entry.UserId = principal.UserId;

                // 2. Pool Validation
                if (string.IsNullOrEmpty(entry.PoolId))
                {
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteStringAsync("Pool ID is required.");
                    return badReq;
                }

                var pool = await _cosmosService.GetPoolAsync(entry.PoolId);
                if (pool == null) return req.CreateResponse(HttpStatusCode.NotFound);

                // 3. Deadline Check (Lifecycle)
                if (DateTime.UtcNow > pool.LockDate)
                {
                    var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                    await forbidden.WriteStringAsync("This pool is locked. No more changes allowed.");
                    return forbidden;
                }

                // 4. Access Check (Invite Code for NEW entries)
                var existingEntry = await _cosmosService.GetEntryByUserAsync(principal.UserId, entry.PoolId);
                if (existingEntry == null)
                {
                    // This is a join attempt. Check the code.
                    var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                    string? suppliedCode = query["inviteCode"];

                    // Case-insensitive compare
                    if (!string.Equals(pool.InviteCode, suppliedCode, StringComparison.OrdinalIgnoreCase))
                    {
                        var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                        await forbidden.WriteStringAsync("Invalid Invite Code.");
                        return forbidden;
                    }
                }
                else
                {
                    // Updating existing entry. Ensure ID consistency.
                    entry.Id = existingEntry.Id;
                    entry.CreatedOn = existingEntry.CreatedOn; // Preserve original timestamp
                }

                // 5. Completeness Check (All-or-Nothing)
                var allGames = await _cosmosService.GetGamesAsync();
                if (entry.Picks.Count != allGames.Count)
                {
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteStringAsync($"Incomplete Entry. You must pick all {allGames.Count} games.");
                    return badReq;
                }

                // Save
                await _cosmosService.AddEntryAsync(entry);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(entry);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SaveMyEntry failed.");
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }

        // ADMIN: Save Entry (Override)
        [Function("SaveEntry")]
        public async Task<HttpResponseData> SaveEntry([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            _logger.LogInformation("Admin saving entry.");
            try
            {
                var authResult = await SecurityHelper.ValidateSuperAdminAsync(req, _cosmosService);
                if (!authResult.IsValid) return authResult.ErrorResponse!;

                var entry = await JsonSerializer.DeserializeAsync<BracketEntry>(req.Body);
                if (entry == null || string.IsNullOrWhiteSpace(entry.PlayerName))
                {
                    return req.CreateResponse(HttpStatusCode.BadRequest);
                }

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

        // ADMIN: Delete Entry
        [Function("DeleteEntry")]
        public async Task<HttpResponseData> DeleteEntry([HttpTrigger(AuthorizationLevel.Anonymous, "delete")] HttpRequestData req)
        {
            _logger.LogInformation("Admin deleting entry.");
            try
            {
                var authResult = await SecurityHelper.ValidateSuperAdminAsync(req, _cosmosService);
                if (!authResult.IsValid) return authResult.ErrorResponse!;

                var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                var id = query["id"];

                if (string.IsNullOrEmpty(id)) return req.CreateResponse(HttpStatusCode.BadRequest);

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
