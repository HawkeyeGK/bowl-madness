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
            var poolId = query["poolId"];

            if (string.IsNullOrEmpty(poolId)) return req.CreateResponse(HttpStatusCode.BadRequest);

            var entries = await _cosmosService.GetEntriesForUserAsync(principal.UserId, poolId);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(entries);
            return response;
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

                if (string.IsNullOrEmpty(entry.PoolId))
                {
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteStringAsync("Pool ID is required.");
                    return badReq;
                }

                // 1. Check Pool Status
                var pool = await _cosmosService.GetPoolAsync(entry.PoolId);
                if (pool == null) return req.CreateResponse(HttpStatusCode.NotFound);

                if (DateTime.UtcNow > pool.LockDate)
                {
                    var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                    await forbidden.WriteStringAsync("This pool is locked. No more changes allowed.");
                    return forbidden;
                }

                // 2. Determine Create vs Update
                bool isUpdate = false;
                if (!string.IsNullOrEmpty(entry.Id))
                {
                    var existingEntry = await _cosmosService.GetEntryAsync(entry.Id);
                    
                    if (existingEntry != null)
                    {
                        if (existingEntry.UserId != principal.UserId)
                        {
                            return req.CreateResponse(HttpStatusCode.Forbidden); 
                        }
                        isUpdate = true;
                        entry.CreatedOn = existingEntry.CreatedOn;
                    }
                }

                // 3. Invite Code Check (Skipped if user already has verified entries)
                if (!isUpdate)
                {
                    // Check if they are already in the pool
                    var userEntries = await _cosmosService.GetEntriesForUserAsync(principal.UserId, entry.PoolId);
                    bool alreadyJoined = userEntries.Any();

                    if (!alreadyJoined)
                    {
                        // First time entering? Check the code.
                        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                        string? suppliedCode = query["inviteCode"];

                        if (!string.IsNullOrEmpty(pool.InviteCode) && 
                            !string.Equals(pool.InviteCode, suppliedCode, StringComparison.OrdinalIgnoreCase))
                        {
                            var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                            await forbidden.WriteStringAsync("Invalid Invite Code.");
                            return forbidden;
                        }
                    }
                }

                // 4. Name Uniqueness Check
                if (string.IsNullOrWhiteSpace(entry.PlayerName))
                {
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteStringAsync("Bracket Name is required.");
                    return badReq;
                }

                bool isNameTaken = await _cosmosService.IsBracketNameTakenAsync(entry.PoolId, entry.PlayerName, isUpdate ? entry.Id : null);
                if (isNameTaken)
                {
                    var conflict = req.CreateResponse(HttpStatusCode.Conflict);
                    await conflict.WriteStringAsync($"The bracket name '{entry.PlayerName}' is already taken. Please choose another.");
                    return conflict;
                }

                // 5. Game Count Validation
                var allGames = await _cosmosService.GetGamesAsync();
                if (entry.Picks.Count != allGames.Count)
                {
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteStringAsync($"Incomplete Entry. You must pick all {allGames.Count} games.");
                    return badReq;
                }

                // 6. Save
                if (!isUpdate)
                {
                    entry.Id = Guid.NewGuid().ToString();
                }

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
