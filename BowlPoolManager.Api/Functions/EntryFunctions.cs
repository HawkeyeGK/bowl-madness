using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core;
using System.Text.Json;
using BowlPoolManager.Api.Helpers;
using BowlPoolManager.Api.Repositories; // NEW
using BowlPoolManager.Core.Helpers; // For ScoringEngine if used

namespace BowlPoolManager.Api.Functions
{
    public class EntryFunctions
    {
        private readonly ILogger _logger;
        
        // REPLACED: private readonly ICosmosDbService _cosmosService;
        private readonly IEntryRepository _entryRepo;
        private readonly IPoolRepository _poolRepo;
        private readonly IGameRepository _gameRepo;
        private readonly IUserRepository _userRepo;

        public EntryFunctions(ILoggerFactory loggerFactory, 
                              IEntryRepository entryRepo,
                              IPoolRepository poolRepo,
                              IGameRepository gameRepo,
                              IUserRepository userRepo)
        {
            _logger = loggerFactory.CreateLogger<EntryFunctions>();
            _entryRepo = entryRepo;
            _poolRepo = poolRepo;
            _gameRepo = gameRepo;
            _userRepo = userRepo;
        }

        [Function("GetEntries")]
        public async Task<HttpResponseData> GetEntries([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            _logger.LogInformation("Getting entries.");
            var poolId = req.Query["poolId"];

            // Use Entry Repo
            var entries = await _entryRepo.GetEntriesAsync(poolId);
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(entries);
            return response;
        }

        [Function("GetMyEntries")]
        public async Task<HttpResponseData> GetMyEntries([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            var principal = SecurityHelper.ParseSwaHeader(req);
            if (principal == null) return req.CreateResponse(HttpStatusCode.Unauthorized);

            var poolId = req.Query["poolId"]; // Optional filter

            // Use Entry Repo
            var entries = await _entryRepo.GetEntriesForUserAsync(principal.UserId, poolId ?? "");
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(entries);
            return response;
        }

        [Function("GetEntry")]
        public async Task<HttpResponseData> GetEntry([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            var id = req.Query["id"];
            if (string.IsNullOrEmpty(id)) return req.CreateResponse(HttpStatusCode.BadRequest);

            // Use Entry Repo
            var entry = await _entryRepo.GetEntryAsync(id);
            
            if (entry == null) return req.CreateResponse(HttpStatusCode.NotFound);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(entry);
            return response;
        }

        [Function("CreateEntry")]
        public async Task<HttpResponseData> CreateEntry([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            _logger.LogInformation("Creating/Updating Entry.");
            
            var principal = SecurityHelper.ParseSwaHeader(req);
            if (principal == null) return req.CreateResponse(HttpStatusCode.Unauthorized);

            var entry = await JsonSerializer.DeserializeAsync<BracketEntry>(req.Body);
            if (entry == null) return req.CreateResponse(HttpStatusCode.BadRequest);

            // 1. Validate Pool & Lock Date
            // Use Pool Repo
            var pool = await _poolRepo.GetPoolAsync(entry.PoolId);
            if (pool == null)
            {
                var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                await badReq.WriteStringAsync("Invalid Pool ID.");
                return badReq;
            }

            if (DateTime.UtcNow > pool.LockDate)
            {
                var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                await badReq.WriteStringAsync("This pool is locked. No new picks or changes allowed.");
                return badReq;
            }

            // 2. Validate Ownership (if updating existing)
            if (!string.IsNullOrEmpty(entry.Id))
            {
                // Use Entry Repo
                var existing = await _entryRepo.GetEntryAsync(entry.Id);
                // If it exists, check ownership
                if (existing != null && existing.UserId != principal.UserId)
                {
                    // Allow SuperAdmin override, otherwise block
                    // Use User Repo for permission check
                    var user = await _userRepo.GetUserAsync(principal.UserId);
                    if (user == null || user.AppRole != Constants.Roles.SuperAdmin)
                    {
                        return req.CreateResponse(HttpStatusCode.Forbidden);
                    }
                }
            }

            // 3. Set Owner & Metadata
            if (string.IsNullOrEmpty(entry.UserId)) entry.UserId = principal.UserId;
            
            // 4. Check Name Uniqueness (in this pool)
            // Use Entry Repo
            bool isTaken = await _entryRepo.IsBracketNameTakenAsync(entry.PoolId, entry.PlayerName, entry.Id);
            if (isTaken)
            {
                var conflict = req.CreateResponse(HttpStatusCode.Conflict);
                await conflict.WriteStringAsync("Bracket Name is already taken in this pool.");
                return conflict;
            }

            // 5. Save
            // Use Entry Repo
            await _entryRepo.AddEntryAsync(entry);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(entry);
            return response;
        }

        [Function("DeleteEntry")]
        public async Task<HttpResponseData> DeleteEntry([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            var principal = SecurityHelper.ParseSwaHeader(req);
            if (principal == null) return req.CreateResponse(HttpStatusCode.Unauthorized);

            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var id = query["id"];
            if (string.IsNullOrEmpty(id)) return req.CreateResponse(HttpStatusCode.BadRequest);

            // Use Entry Repo
            var entry = await _entryRepo.GetEntryAsync(id);
            if (entry == null) return req.CreateResponse(HttpStatusCode.NotFound);

            // Auth Check: Owner or SuperAdmin
            if (entry.UserId != principal.UserId)
            {
                // Use User Repo
                var user = await _userRepo.GetUserAsync(principal.UserId);
                if (user == null || user.AppRole != Constants.Roles.SuperAdmin)
                {
                    return req.CreateResponse(HttpStatusCode.Forbidden);
                }
            }

            // Use Pool Repo to check Lock Date (Prevent deleting after lock?)
            // Policy: Usually allow delete, or block? Assuming block if locked for fairness.
            var pool = await _poolRepo.GetPoolAsync(entry.PoolId);
            if (pool != null && DateTime.UtcNow > pool.LockDate)
            {
                // Allow SuperAdmin to delete even after lock
                var user = await _userRepo.GetUserAsync(principal.UserId);
                if (user == null || user.AppRole != Constants.Roles.SuperAdmin)
                {
                    var lockedResp = req.CreateResponse(HttpStatusCode.BadRequest);
                    await lockedResp.WriteStringAsync("Cannot delete entry after pool is locked.");
                    return lockedResp;
                }
            }

            // Use Entry Repo
            await _entryRepo.DeleteEntryAsync(id);
            return req.CreateResponse(HttpStatusCode.OK);
        }
    }
}
