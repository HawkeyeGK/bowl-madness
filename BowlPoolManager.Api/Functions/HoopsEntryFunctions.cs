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
using BowlPoolManager.Core.Validation;

namespace BowlPoolManager.Api.Functions
{
    public class HoopsEntryFunctions
    {
        private readonly ILogger _logger;
        private readonly IHoopsEntryRepository _entryRepo;
        private readonly IHoopsPoolRepository _poolRepo;
        private readonly IHoopsGameRepository _gameRepo;
        private readonly IUserRepository _userRepo;

        public HoopsEntryFunctions(
            ILoggerFactory loggerFactory,
            IHoopsEntryRepository entryRepo,
            IHoopsPoolRepository poolRepo,
            IHoopsGameRepository gameRepo,
            IUserRepository userRepo)
        {
            _logger = loggerFactory.CreateLogger<HoopsEntryFunctions>();
            _entryRepo = entryRepo;
            _poolRepo = poolRepo;
            _gameRepo = gameRepo;
            _userRepo = userRepo;
        }

        [Function("GetHoopsEntries")]
        public async Task<HttpResponseData> GetHoopsEntries(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            var poolId = req.Query["poolId"];
            var seasonId = req.Query["seasonId"];

            var caller = await SecurityHelper.GetCallerAsync(req, _userRepo);

            // Only an Admin may list across seasons or pools; everyone else names both (issue 97).
            if (caller?.IsAdmin != true && (string.IsNullOrEmpty(seasonId) || string.IsNullOrEmpty(poolId)))
            {
                var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                await badReq.WriteStringAsync("Both seasonId and poolId are required.");
                return badReq;
            }

            var entries = await _entryRepo.GetEntriesAsync(seasonId, poolId);

            if (caller?.IsAdmin != true)
            {
                // Each entry is judged against its own pool's lock date.
                var lockedByPool = new Dictionary<string, bool>();
                var visible = new List<BracketEntry>();
                foreach (var entry in entries)
                {
                    if (!lockedByPool.TryGetValue(entry.PoolId, out var locked))
                    {
                        var pool = await _poolRepo.GetPoolAsync(entry.PoolId);
                        locked = pool != null && EntryVisibility.IsLocked(pool.LockDate);
                        lockedByPool[entry.PoolId] = locked;
                    }
                    visible.Add(EntryVisibility.ForViewer(entry, locked, caller));
                }
                entries = visible;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(entries);
            return response;
        }

        [Function("GetMyHoopsEntries")]
        public async Task<HttpResponseData> GetMyHoopsEntries(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            var caller = await SecurityHelper.GetCallerAsync(req, _userRepo);
            if (caller == null) return req.CreateResponse(HttpStatusCode.Unauthorized);

            var poolId = req.Query["poolId"] ?? "";
            var seasonId = req.Query["seasonId"];

            var entries = await _entryRepo.GetEntriesForUserAsync(caller.UserId, seasonId, poolId);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(entries);
            return response;
        }

        [Function("GetHoopsEntry")]
        public async Task<HttpResponseData> GetHoopsEntry(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            var id = req.Query["id"];
            var seasonId = req.Query["seasonId"];
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(seasonId))
                return req.CreateResponse(HttpStatusCode.BadRequest);

            var entry = await _entryRepo.GetEntryAsync(id, seasonId);
            if (entry == null) return req.CreateResponse(HttpStatusCode.NotFound);

            var caller = await SecurityHelper.GetCallerAsync(req, _userRepo);
            if (caller?.IsAdmin != true)
            {
                var pool = await _poolRepo.GetPoolAsync(entry.PoolId);
                var locked = pool != null && EntryVisibility.IsLocked(pool.LockDate);
                entry = EntryVisibility.ForViewer(entry, locked, caller);
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(entry);
            return response;
        }

        [Function("CreateHoopsEntry")]
        public async Task<HttpResponseData> CreateHoopsEntry(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            try
            {
                _logger.LogInformation("Creating/Updating Hoops Entry.");

                var caller = await SecurityHelper.GetCallerAsync(req, _userRepo);
                if (caller == null)
                {
                    var unauth = req.CreateResponse(HttpStatusCode.Unauthorized);
                    await unauth.WriteStringAsync("User is not authenticated.");
                    return unauth;
                }

                var userProfile = caller.Profile;
                bool isAdmin = caller.IsAdmin;

                BracketEntry? submitted = null;
                try { submitted = await JsonSerializer.DeserializeAsync<BracketEntry>(req.Body); }
                catch { /* submitted remains null */ }

                if (submitted == null)
                {
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteStringAsync("Invalid entry data.");
                    return badReq;
                }

                var pool = await _poolRepo.GetPoolAsync(submitted.PoolId);
                if (pool == null)
                {
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteStringAsync("Invalid Pool ID.");
                    return badReq;
                }

                BracketEntry? existingEntry = null;
                if (!string.IsNullOrEmpty(submitted.Id))
                {
                    existingEntry = await _entryRepo.GetEntryAsync(submitted.Id, pool.SeasonId);
                    if (existingEntry != null && existingEntry.UserId != caller.UserId && !isAdmin)
                    {
                        var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                        await forbidden.WriteStringAsync("You do not have permission to modify this entry.");
                        return forbidden;
                    }

                    // A player's save never moves an entry to another pool; the lock check would run against the wrong
                    // pool (issue 131). An Admin moves entries on purpose.
                    if (existingEntry != null && !isAdmin && existingEntry.PoolId != submitted.PoolId)
                    {
                        var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                        await badReq.WriteStringAsync("An entry cannot be moved to another pool.");
                        return badReq;
                    }
                }

                var validationResult = HoopsEntryUpdateValidator.ValidateUpdate(pool, existingEntry, submitted, isAdmin);
                if (!validationResult.IsValid)
                {
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteStringAsync(validationResult.ErrorMessage);
                    return badReq;
                }

                // Games this pool uses: its own list, or every game of the season when it has none.
                var allGames = await _gameRepo.GetGamesAsync(pool.SeasonId);
                var poolGameIds = pool.GameIds != null && pool.GameIds.Any()
                    ? pool.GameIds
                    : allGames.Select(g => g.Id).ToList();

                var fieldResult = EntrySubmission.Validate(submitted, existingEntry, poolGameIds);
                if (!fieldResult.IsValid)
                {
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteStringAsync(fieldResult.ErrorMessage);
                    return badReq;
                }

                var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                bool assignToMe = string.Equals(queryParams["assignToMe"], "true", StringComparison.OrdinalIgnoreCase);

                // Only the name, picks and tiebreaker come from the browser; the rest is the stored entry or the server.
                var entry = EntrySubmission.Build(submitted, existingEntry, pool.Id, pool.SeasonId,
                    Constants.DocumentTypes.HoopsBracketEntry, caller.UserId, isAdmin, assignToMe);

                // Build audit log
                string userEmail = userProfile?.Email ?? "Unknown User";
                string timestamp = DateTimeHelper.ToCentral(DateTime.UtcNow).ToString("MMM d, yyyy h:mm tt");

                if (existingEntry == null)
                {
                    entry.AuditLog = new List<string>
                    {
                        $"Entry Created by {userEmail} on {timestamp}"
                    };
                }
                else
                {
                    var changes = new List<string>();

                    if (!string.Equals(existingEntry.PlayerName, entry.PlayerName, StringComparison.Ordinal))
                        changes.Add($"Changed Bracket Name: {existingEntry.PlayerName} -> {entry.PlayerName}");

                    var oldPicks = existingEntry.Picks ?? new Dictionary<string, string>();
                    var newPicks = entry.Picks ?? new Dictionary<string, string>();
                    var allKeys = oldPicks.Keys.Union(newPicks.Keys).Distinct();

                    foreach (var gameId in allKeys)
                    {
                        oldPicks.TryGetValue(gameId, out var oldPick);
                        newPicks.TryGetValue(gameId, out var newPick);

                        if (!string.Equals(oldPick, newPick, StringComparison.OrdinalIgnoreCase))
                        {
                            var game = allGames.FirstOrDefault(g => g.Id == gameId);
                            string gameName = game != null
                                ? $"{game.Round} {game.Region} {game.SeedMatchup}".Trim()
                                : "Unknown Game";
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
                _logger.LogError(ex, "Error creating hoops entry");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteStringAsync($"Internal Server Error: {ex.Message}");
                return error;
            }
        }

        [Function("DeleteHoopsEntry")]
        public async Task<HttpResponseData> DeleteHoopsEntry(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "DeleteHoopsEntry/{id}")] HttpRequestData req,
            string id)
        {
            try
            {
                var caller = await SecurityHelper.GetCallerAsync(req, _userRepo);
                if (caller == null)
                {
                    var unauth = req.CreateResponse(HttpStatusCode.Unauthorized);
                    await unauth.WriteStringAsync("User is not authenticated.");
                    return unauth;
                }

                if (string.IsNullOrEmpty(id))
                {
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteStringAsync("Missing ID parameter.");
                    return badReq;
                }

                var seasonId = req.Query["seasonId"];
                if (string.IsNullOrEmpty(seasonId))
                {
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteStringAsync("Season ID is required.");
                    return badReq;
                }

                var entry = await _entryRepo.GetEntryAsync(id, seasonId);
                if (entry == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync("Entry not found.");
                    return notFound;
                }

                if (entry.UserId != caller.UserId)
                {
                    if (!caller.IsAdmin)
                    {
                        var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                        await forbidden.WriteStringAsync("You do not have permission to delete this entry.");
                        return forbidden;
                    }
                }

                var pool = await _poolRepo.GetPoolAsync(entry.PoolId);
                if (pool != null && DateTime.UtcNow > pool.LockDate)
                {
                    if (!caller.IsAdmin)
                    {
                        var lockedResp = req.CreateResponse(HttpStatusCode.BadRequest);
                        await lockedResp.WriteStringAsync("Cannot delete entry after pool is locked.");
                        return lockedResp;
                    }
                }

                await _entryRepo.DeleteEntryAsync(id, seasonId);
                return req.CreateResponse(HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting hoops entry");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteStringAsync($"Internal Server Error: {ex.Message}");
                return error;
            }
        }

        [Function("SetHoopsEntryPaidStatus")]
        public async Task<HttpResponseData> SetHoopsEntryPaidStatus(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            try
            {
                var caller = await SecurityHelper.GetCallerAsync(req, _userRepo);
                if (caller == null)
                {
                    var unauth = req.CreateResponse(HttpStatusCode.Unauthorized);
                    await unauth.WriteStringAsync("User is not authenticated.");
                    return unauth;
                }

                if (!caller.IsAdmin)
                {
                    var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                    await forbidden.WriteStringAsync("You must be an admin to update payment status.");
                    return forbidden;
                }

                var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                var id = query["id"];
                var isPaidStr = query["isPaid"];
                var seasonId = query["seasonId"];

                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(isPaidStr) || string.IsNullOrEmpty(seasonId))
                {
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteStringAsync("Missing required parameters.");
                    return badReq;
                }

                if (!bool.TryParse(isPaidStr, out bool isPaid))
                {
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteStringAsync("Invalid isPaid parameter.");
                    return badReq;
                }

                var entry = await _entryRepo.GetEntryAsync(id, seasonId);
                if (entry == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync("Entry not found.");
                    return notFound;
                }

                entry.IsPaid = isPaid;
                await _entryRepo.AddEntryAsync(entry);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(entry);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating hoops entry payment status");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteStringAsync($"Internal Server Error: {ex.Message}");
                return error;
            }
        }
    }
}
