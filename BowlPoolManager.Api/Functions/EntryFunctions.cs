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
            
            var pools = await _cosmosService.GetPoolsAsync();
            var poolLockMap = pools.ToDictionary(p => p.Id, p => p.LockDate);

            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            string? poolIdFilter = query["poolId"];
            
            // NOTE: Leaderboard always filters by poolId, but logic remains valid if global view needed later
            var entries = await _cosmosService.GetEntriesAsync(poolIdFilter);

            // Security Check
            var principal = SecurityHelper.ParseSwaHeader(req);
            string currentUserId = principal?.UserId ?? string.Empty;
            bool isAdmin = await SecurityHelper.IsSuperAdminAsync(req, _cosmosService);

            // The Silencer (Redaction Logic)
            foreach (var entry in entries)
            {
                if (isAdmin) continue; 
                if (!string.IsNullOrEmpty(entry.UserId) && entry.UserId == currentUserId) continue; 

                bool isPoolOpen = true; 
                if (!string.IsNullOrEmpty(entry.PoolId) && poolLockMap.TryGetValue(entry.PoolId, out var lockDate))
                {
                    isPoolOpen = DateTime.UtcNow < lockDate;
                }

                if (isPoolOpen)
                {
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

            var principal = SecurityHelper.ParseSwaHeader(req);
            string currentUserId = principal?.UserId ?? string.Empty;
            bool isAdmin = await SecurityHelper.IsSuperAdminAsync(req, _cosmosService);

            bool isMine = !string.IsNullOrEmpty(entry.UserId) && entry.UserId == currentUserId;
            
            if (!isAdmin && !isMine)
            {
                if (!string.IsNullOrEmpty(entry.PoolId))
                {
                    var pool = await _cosmosService.GetPoolAsync(entry.PoolId);
                    if (pool != null && DateTime.UtcNow < pool.LockDate)
                    {
                        entry.Picks = null;
                        entry.TieBreakerPoints = 0;
                    }
                }
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(entry);
            return response;
        }

        [Function("GetMyEntries")]
        public async Task<HttpResponseData> GetMyEntries([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            var principal = SecurityHelper.ParseSwaHeader(req);
            if (principal == null || string.IsNullOrEmpty(principal.UserId))
            {
                return req.CreateResponse(HttpStatusCode.Unauthorized);
            }

            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            string? poolId = query["poolId"]; // Nullable: If null, return ALL entries for user

            var entries = await _cosmosService.GetEntriesForUserAsync(principal.UserId, poolId ?? "");

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(entries);
            return response;
        }

        [Function("DeleteMyEntry")]
        public async Task<HttpResponseData> DeleteMyEntry([HttpTrigger(AuthorizationLevel.Anonymous, "delete")] HttpRequestData req)
        {
            _logger.LogInformation("User attempting to delete their entry.");
            
            var principal = SecurityHelper.ParseSwaHeader(req);
            if (principal == null || string.IsNullOrEmpty(principal.UserId))
            {
                return req.CreateResponse(HttpStatusCode.Unauthorized);
            }

            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var id = query["id"];

            if (string.IsNullOrEmpty(id)) return req.CreateResponse(HttpStatusCode.BadRequest);

            var entry = await _cosmosService.GetEntryAsync(id);
            if (entry == null) return req.CreateResponse(HttpStatusCode.NotFound);

            // 1. Verify Ownership
            if (entry.UserId != principal.UserId)
            {
                return req.CreateResponse(HttpStatusCode.Forbidden);
            }

            // 2. Verify Pool Lock Status
            if (!string.IsNullOrEmpty(entry.PoolId))
            {
                var pool = await _cosmosService.GetPoolAsync(entry.PoolId);
                if (pool != null && DateTime.UtcNow > pool.LockDate)
                {
                    var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                    await forbidden.WriteStringAsync("Cannot delete entry. The pool is locked.");
                    return forbidden;
                }
            }

            await _cosmosService.DeleteEntryAsync(id);
            return req.CreateResponse(HttpStatusCode.OK);
        }

        [Function("SaveMyEntry")]
        public async Task<HttpResponseData> SaveMyEntry([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            _logger.LogInformation("User attempting to save entry.");

            try
            {
                var principal = SecurityHelper.ParseSwaHeader(req);
                if (principal == null || string.IsNullOrEmpty(principal.UserId))
                {
                    return req.CreateResponse(HttpStatusCode.Unauthorized);
                }

                var entry = await JsonSerializer.DeserializeAsync<BracketEntry>(req.Body);
                if (entry == null) return req.CreateResponse(HttpStatusCode.BadRequest);

                // Enforce User Ownership
                entry.UserId = principal.UserId;

                // --- 1. RESOLVE POOL ID ---
                BowlPool? pool = null;

                // Case A: Existing Entry (Update)
                if (!string.IsNullOrEmpty(entry.Id))
                {
                    var existingEntry = await _cosmosService.GetEntryAsync(entry.Id);
                    if (existingEntry != null)
                    {
                        if (existingEntry.UserId != principal.UserId) return req.CreateResponse(HttpStatusCode.Forbidden);
                        
                        entry.PoolId = existingEntry.PoolId; // Cannot switch pools on update
                        entry.CreatedOn = existingEntry.CreatedOn;
                    }
                    else
                    {
                        return req.CreateResponse(HttpStatusCode.NotFound);
                    }
                }
                // Case B: New Entry (Create)
                else
                {
                    // If PoolId is missing, we MUST have an invite code to find it
                    if (string.IsNullOrEmpty(entry.PoolId))
                    {
                        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                        string? suppliedCode = query["inviteCode"];

                        if (string.IsNullOrWhiteSpace(suppliedCode))
                        {
                            var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                            await badReq.WriteStringAsync("Invite Code is required to join a pool.");
                            return badReq;
                        }

                        pool = await _cosmosService.GetPoolByInviteCodeAsync(suppliedCode);
                        if (pool == null)
                        {
                            var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                            await badReq.WriteStringAsync("Invalid Invite Code. Pool not found.");
                            return badReq;
                        }
                        
                        entry.PoolId = pool.Id;
                        entry.Id = Guid.NewGuid().ToString();
                    }
                }

                // If pool wasn't loaded yet (Update case), load it now
                if (pool == null)
                {
                    pool = await _cosmosService.GetPoolAsync(entry.PoolId);
                    if (pool == null) return req.CreateResponse(HttpStatusCode.NotFound);
                }

                // --- 2. LOCK CHECK ---
                if (DateTime.UtcNow > pool.LockDate)
                {
                    var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                    await forbidden.WriteStringAsync("This pool is locked. No more changes allowed.");
                    return forbidden;
                }

                // --- 3. DUPLICATE CHECK ---
                // Only check if name changed or new
                bool isNameTaken = await _cosmosService.IsBracketNameTakenAsync(entry.PoolId, entry.PlayerName, entry.Id);
                if (isNameTaken)
                {
                    var conflict = req.CreateResponse(HttpStatusCode.Conflict);
                    await conflict.WriteStringAsync($"The bracket name '{entry.PlayerName}' is already taken in the '{pool.Name}' pool.");
                    return conflict;
                }

                // --- 4. GAME COUNT CHECK ---
                var allGames = await _cosmosService.GetGamesAsync();
                if (entry.Picks.Count != allGames.Count)
                {
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteStringAsync($"Incomplete Entry. You must pick all {allGames.Count} games.");
                    return badReq;
                }

                // --- 5. SAVE ---
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

        [Function("MoveEntry")]
        public async Task<HttpResponseData> MoveEntry([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            _logger.LogInformation("Admin moving entry to new pool.");
            try
            {
                // SuperAdmin Only
                var authResult = await SecurityHelper.ValidateSuperAdminAsync(req, _cosmosService);
                if (!authResult.IsValid) return authResult.ErrorResponse!;

                var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                string? entryId = query["entryId"];
                string? newPoolId = query["newPoolId"];

                if (string.IsNullOrEmpty(entryId) || string.IsNullOrEmpty(newPoolId)) 
                    return req.CreateResponse(HttpStatusCode.BadRequest);

                var entry = await _cosmosService.GetEntryAsync(entryId);
                if (entry == null) return req.CreateResponse(HttpStatusCode.NotFound);

                var newPool = await _cosmosService.GetPoolAsync(newPoolId);
                if (newPool == null) 
                {
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteStringAsync("Target Pool not found.");
                    return badReq;
                }

                // Name Conflict Check in Destination
                bool isTaken = await _cosmosService.IsBracketNameTakenAsync(newPoolId, entry.PlayerName, entry.Id);
                if (isTaken)
                {
                    var conflict = req.CreateResponse(HttpStatusCode.Conflict);
                    await conflict.WriteStringAsync($"Bracket name '{entry.PlayerName}' is already taken in the destination pool '{newPool.Name}'.");
                    return conflict;
                }

                // Move
                entry.PoolId = newPoolId;
                await _cosmosService.AddEntryAsync(entry); // Upsert

                return req.CreateResponse(HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MoveEntry failed.");
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }
        
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
