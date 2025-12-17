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
            _logger.LogInformation("Getting all bracket entries with security check.");
            
            // 1. Fetch Data
            // We fetch ALL pools to build a "Lock Map" (PoolId -> LockDate) efficiently
            var pools = await _cosmosService.GetPoolsAsync();
            var poolLockMap = pools.ToDictionary(p => p.Id, p => p.LockDate);

            // Fetch Entries (Optionally filtered)
            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            string? poolIdFilter = query["poolId"];
            var entries = await _cosmosService.GetEntriesAsync(poolIdFilter);

            // 2. Identify Requester
            var principal = SecurityHelper.ParseSwaHeader(req);
            string currentUserId = principal?.UserId ?? string.Empty;
            bool isAdmin = principal != null && principal.UserRoles.Contains(Constants.Roles.SuperAdmin);

            // 3. The Silencer (Redaction Logic)
            foreach (var entry in entries)
            {
                // Rule 1: Admins see everything.
                if (isAdmin) continue;

                // Rule 2: I see my own picks.
                if (!string.IsNullOrEmpty(entry.UserId) && entry.UserId == currentUserId) continue;

                // Rule 3: Check Lock Date
                bool isPoolOpen = true; // Default to Open (Secure) if unknown
                if (!string.IsNullOrEmpty(entry.PoolId) && poolLockMap.TryGetValue(entry.PoolId, out var lockDate))
                {
                    isPoolOpen = DateTime.UtcNow < lockDate;
                }

                if (isPoolOpen)
                {
                    // REDACT: Hide the data so cheating is impossible
                    entry.Picks = null; 
                    entry.TieBreakerPoints = 0;
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

            // SECURITY CHECK
            // 1. Identify Requester
            var principal = SecurityHelper.ParseSwaHeader(req);
            string currentUserId = principal?.UserId ?? string.Empty;
            bool isAdmin = principal != null && principal.UserRoles.Contains(Constants.Roles.SuperAdmin);

            // 2. Check Ownership/Admin
            bool isMine = !string.IsNullOrEmpty(entry.UserId) && entry.UserId == currentUserId;
            
            if (!isAdmin && !isMine)
            {
                // 3. Check Lock Date
                if (!string.IsNullOrEmpty(entry.PoolId))
                {
                    var pool = await _cosmosService.GetPoolAsync(entry.PoolId);
                    if (pool != null)
                    {
                        bool isPoolOpen = DateTime.UtcNow < pool.LockDate;
                        if (isPoolOpen)
                        {
                            // REDACT
                            entry.Picks = null;
                            entry.TieBreakerPoints = 0;
                        }
                    }
                }
            }

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
