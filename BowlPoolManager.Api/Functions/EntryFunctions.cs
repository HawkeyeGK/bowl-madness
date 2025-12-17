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
            var pools = await _cosmosService.GetPoolsAsync();
            var poolLockMap = pools.ToDictionary(p => p.Id, p => p.LockDate);

            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            string? poolIdFilter = query["poolId"];
            var entries = await _cosmosService.GetEntriesAsync(poolIdFilter);

            // 2. Identify Requester & Resolve Role
            var principal = SecurityHelper.ParseSwaHeader(req);
            string currentUserId = principal?.UserId ?? string.Empty;
            bool isAdmin = false;

            // FIXED: Check Database for Admin Role (SWA Header is not enough)
            if (!string.IsNullOrEmpty(currentUserId))
            {
                var userProfile = await _cosmosService.GetUserAsync(currentUserId);
                if (userProfile != null && userProfile.AppRole == Constants.Roles.SuperAdmin)
                {
                    isAdmin = true;
                }
            }

            // 3. The Silencer (Redaction Logic)
            foreach (var entry in entries)
            {
                if (isAdmin) continue; // Admins see all
                if (!string.IsNullOrEmpty(entry.UserId) && entry.UserId == currentUserId) continue; // Owners see their own

                // Check Lock Date
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

            // SECURITY CHECK
            var principal = SecurityHelper.ParseSwaHeader(req);
            string currentUserId = principal?.UserId ?? string.Empty;
            bool isAdmin = false;

            // FIXED: Check Database for Admin Role
            if (!string.IsNullOrEmpty(currentUserId))
            {
                var userProfile = await _cosmosService.GetUserAsync(currentUserId);
                if (userProfile != null && userProfile.AppRole == Constants.Roles.SuperAdmin)
                {
                    isAdmin = true;
                }
            }

            // Check Ownership/Admin
            bool isMine = !string.IsNullOrEmpty(entry.UserId) && entry.UserId == currentUserId;
            
            if (!isAdmin && !isMine)
            {
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

            if (entry == null) return req.CreateResponse(HttpStatusCode.NotFound);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(entry);
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

                entry.UserId = principal.UserId;

                if (string.IsNullOrEmpty(entry.PoolId))
                {
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteStringAsync("Pool ID is required.");
                    return badReq;
                }

                var pool = await _cosmosService.GetPoolAsync(entry.PoolId);
                if (pool == null) return req.CreateResponse(HttpStatusCode.NotFound);

                if (DateTime.UtcNow > pool.LockDate)
                {
                    var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                    await forbidden.WriteStringAsync("This pool is locked. No more changes allowed.");
                    return forbidden;
                }

                var existingEntry = await _cosmosService.GetEntryByUserAsync(principal.UserId, entry.PoolId);
                if (existingEntry == null)
                {
                    var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                    string? suppliedCode = query["inviteCode"];

                    if (!string.Equals(pool.InviteCode, suppliedCode, StringComparison.OrdinalIgnoreCase))
                    {
                        var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                        await forbidden.WriteStringAsync("Invalid Invite Code.");
                        return forbidden;
                    }
                }
                else
                {
                    entry.Id = existingEntry.Id;
                    entry.CreatedOn = existingEntry.CreatedOn; 
                }

                var allGames = await _cosmosService.GetGamesAsync();
                if (entry.Picks.Count != allGames.Count)
                {
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteStringAsync($"Incomplete Entry. You must pick all {allGames.Count} games.");
                    return badReq;
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
