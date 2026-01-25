using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core;
using System.Text.Json;
using BowlPoolManager.Api.Helpers;
using BowlPoolManager.Api.Repositories;
using BowlPoolManager.Core.Helpers;

namespace BowlPoolManager.Api.Functions
{
    public class EntryFunctions
    {
        private readonly ILogger _logger;
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

            var poolId = req.Query["poolId"] ?? ""; // SAFE COALESCE

            var entries = await _entryRepo.GetEntriesForUserAsync(principal.UserId, poolId);
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(entries);
            return response;
        }

        [Function("GetEntry")]
        public async Task<HttpResponseData> GetEntry([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            var id = req.Query["id"];
            if (string.IsNullOrEmpty(id)) return req.CreateResponse(HttpStatusCode.BadRequest);

            var entry = await _entryRepo.GetEntryAsync(id);
            
            if (entry == null) return req.CreateResponse(HttpStatusCode.NotFound);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(entry);
            return response;
        }

        [Function("CreateEntry")]
        public async Task<HttpResponseData> CreateEntry([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            try
            {
                _logger.LogInformation("Creating/Updating Entry.");
                
                var principal = SecurityHelper.ParseSwaHeader(req);
                if (principal == null) 
                {
                    var unauth = req.CreateResponse(HttpStatusCode.Unauthorized);
                    await unauth.WriteStringAsync("User is not authenticated.");
                    return unauth;
                }

                // Check Admin Status
                var userProfile = await _userRepo.GetUserAsync(principal.UserId);
                bool isAdmin = SecurityHelper.IsAdmin(userProfile);

                BracketEntry? entry = null;
                try
                {
                    entry = await JsonSerializer.DeserializeAsync<BracketEntry>(req.Body);
                }
                catch
                {
                    // entry remains null
                }

                if (entry == null) 
                {
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteStringAsync("Invalid entry data.");
                    return badReq;
                }

                var pool = await _poolRepo.GetPoolAsync(entry.PoolId);
                if (pool == null)
                {
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteStringAsync("Invalid Pool ID.");
                    return badReq;
                }

                if (DateTime.UtcNow > pool.LockDate && !isAdmin)
                {
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteStringAsync("This pool is locked. No new picks or changes allowed.");
                    return badReq;
                }

                BracketEntry? existingEntry = null;
                if (!string.IsNullOrEmpty(entry.Id))
                {
                    existingEntry = await _entryRepo.GetEntryAsync(entry.Id);
                    if (existingEntry != null && existingEntry.UserId != principal.UserId && !isAdmin)
                    {
                        var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                        await forbidden.WriteStringAsync("You do not have permission to modify this entry.");
                        return forbidden;
                    }
                }

                // AUDIT LOG LOGIC
                string userEmail = userProfile?.Email ?? "Unknown User";
                string timestamp = DateTimeHelper.ToCentral(DateTime.UtcNow).ToString("MMM d, yyyy h:mm tt");

                if (existingEntry == null)
                {
                    // New Entry
                    entry.AuditLog = new List<string>
                    {
                        $"Entry Created by {userEmail} on {timestamp}"
                    };
                }
                else
                {
                    // Update: Compare and Log Changes
                    entry.AuditLog = existingEntry.AuditLog ?? new List<string>();
                    var changes = new List<string>();

                    // 1. Bracket Name
                    if (!string.Equals(existingEntry.PlayerName, entry.PlayerName, StringComparison.Ordinal))
                    {
                        changes.Add($"Changed Bracket Name: {existingEntry.PlayerName} -> {entry.PlayerName}");
                    }

                    // 2. Tiebreaker
                    if (existingEntry.TieBreakerPoints != entry.TieBreakerPoints)
                    {
                        changes.Add($"Changed Tiebreaker: {existingEntry.TieBreakerPoints} -> {entry.TieBreakerPoints}");
                    }

                    // 3. Picks
                    var allGames = await _gameRepo.GetGamesAsync();
                    var existingPicks = existingEntry.Picks ?? new Dictionary<string, string>();
                    var newPicks = entry.Picks ?? new Dictionary<string, string>();
                    var allKeys = existingPicks.Keys.Union(newPicks.Keys).Distinct();

                    foreach (var gameId in allKeys)
                    {
                        existingPicks.TryGetValue(gameId, out var oldPick);
                        newPicks.TryGetValue(gameId, out var newPick);

                        if (!string.Equals(oldPick, newPick, StringComparison.OrdinalIgnoreCase))
                        {
                            var game = allGames.FirstOrDefault(g => g.Id == gameId);
                            string gameName = game != null ? game.BowlName : "Unknown Bowl";
                            string oldVal = string.IsNullOrEmpty(oldPick) ? "No Pick" : oldPick;
                            string newVal = string.IsNullOrEmpty(newPick) ? "No Pick" : newPick;

                            changes.Add($"Changed Winner: {gameName} - {oldVal} -> {newVal}");
                        }
                    }

                    if (changes.Any())
                    {
                        entry.AuditLog.Add($"Changes by {userEmail} on {timestamp}:");
                        entry.AuditLog.AddRange(changes);
                    }
                }

                if (!isAdmin)
                {
                    entry.UserId = principal.UserId;
                }

                if (string.IsNullOrEmpty(entry.Id)) entry.Id = Guid.NewGuid().ToString();

                // FORCE SYNC: Ensure Entry Season matches Pool Season
                entry.SeasonId = pool.SeasonId;

                bool isTaken = await _entryRepo.IsBracketNameTakenAsync(entry.PoolId, entry.PlayerName, entry.Id);
                if (isTaken)
                {
                    var conflict = req.CreateResponse(HttpStatusCode.Conflict);
                    await conflict.WriteStringAsync("Bracket Name is already taken in this pool.");
                    return conflict;
                }

                await _entryRepo.AddEntryAsync(entry);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(entry);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating entry");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteStringAsync($"Internal Server Error: {ex.Message}");
                return error;
            }
        }

        [Function("DeleteEntry")]
        public async Task<HttpResponseData> DeleteEntry([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            try
            {
                var principal = SecurityHelper.ParseSwaHeader(req);
                if (principal == null) 
                {
                    var unauth = req.CreateResponse(HttpStatusCode.Unauthorized);
                    await unauth.WriteStringAsync("User is not authenticated.");
                    return unauth;
                }

                var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                var id = query["id"];
                if (string.IsNullOrEmpty(id)) 
                {
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteStringAsync("Missing ID parameter.");
                    return badReq;
                }

                var entry = await _entryRepo.GetEntryAsync(id);
                if (entry == null) 
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync("Entry not found.");
                    return notFound;
                }

                if (entry.UserId != principal.UserId)
                {
                    var user = await _userRepo.GetUserAsync(principal.UserId);
                    if (!SecurityHelper.IsAdmin(user))
                    {
                        var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                        await forbidden.WriteStringAsync("You do not have permission to delete this entry.");
                        return forbidden;
                    }
                }

                var pool = await _poolRepo.GetPoolAsync(entry.PoolId);
                if (pool != null && DateTime.UtcNow > pool.LockDate)
                {
                    var user = await _userRepo.GetUserAsync(principal.UserId);
                    if (!SecurityHelper.IsAdmin(user))
                    {
                        var lockedResp = req.CreateResponse(HttpStatusCode.BadRequest);
                        await lockedResp.WriteStringAsync("Cannot delete entry after pool is locked.");
                        return lockedResp;
                    }
                }

                await _entryRepo.DeleteEntryAsync(id);
                return req.CreateResponse(HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting entry");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteStringAsync($"Internal Server Error: {ex.Message}");
                return error;
            }
        }
        [Function("SetEntryPaidStatus")]
        public async Task<HttpResponseData> SetEntryPaidStatus([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            try
            {
                var principal = SecurityHelper.ParseSwaHeader(req);
                if (principal == null)
                {
                    var unauth = req.CreateResponse(HttpStatusCode.Unauthorized);
                    await unauth.WriteStringAsync("User is not authenticated.");
                    return unauth;
                }

                // Check Admin Status
                var userProfile = await _userRepo.GetUserAsync(principal.UserId);
                if (!SecurityHelper.IsAdmin(userProfile))
                {
                    var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                    await forbidden.WriteStringAsync("You must be an admin to update payment status.");
                    return forbidden;
                }

                var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                var id = query["id"];
                var isPaidStr = query["isPaid"];

                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(isPaidStr))
                {
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteStringAsync("Missing id or isPaid parameter.");
                    return badReq;
                }

                if (!bool.TryParse(isPaidStr, out bool isPaid))
                {
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteStringAsync("Invalid isPaid parameter.");
                    return badReq;
                }

                var entry = await _entryRepo.GetEntryAsync(id);
                if (entry == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync("Entry not found.");
                    return notFound;
                }

                // Update Status
                entry.IsPaid = isPaid;
                await _entryRepo.AddEntryAsync(entry);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(entry);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating payment status");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteStringAsync($"Internal Server Error: {ex.Message}");
                return error;
            }
        }
    }
}
