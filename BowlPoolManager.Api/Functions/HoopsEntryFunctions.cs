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

            var entries = await _entryRepo.GetEntriesAsync(seasonId, poolId);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(entries);
            return response;
        }

        [Function("GetMyHoopsEntries")]
        public async Task<HttpResponseData> GetMyHoopsEntries(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            var principal = SecurityHelper.ParseSwaHeader(req);
            if (principal == null) return req.CreateResponse(HttpStatusCode.Unauthorized);

            var poolId = req.Query["poolId"] ?? "";
            var seasonId = req.Query["seasonId"];

            var entries = await _entryRepo.GetEntriesForUserAsync(principal.UserId, seasonId, poolId);

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

                var principal = SecurityHelper.ParseSwaHeader(req);
                if (principal == null)
                {
                    var unauth = req.CreateResponse(HttpStatusCode.Unauthorized);
                    await unauth.WriteStringAsync("User is not authenticated.");
                    return unauth;
                }

                var userProfile = await _userRepo.GetUserAsync(principal.UserId);
                bool isAdmin = SecurityHelper.IsAdmin(userProfile);

                BracketEntry? entry = null;
                try { entry = await JsonSerializer.DeserializeAsync<BracketEntry>(req.Body); }
                catch { /* entry remains null */ }

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

                BracketEntry? existingEntry = null;
                if (!string.IsNullOrEmpty(entry.Id))
                {
                    existingEntry = await _entryRepo.GetEntryAsync(entry.Id, pool.SeasonId);
                    if (existingEntry != null && existingEntry.UserId != principal.UserId && !isAdmin)
                    {
                        var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                        await forbidden.WriteStringAsync("You do not have permission to modify this entry.");
                        return forbidden;
                    }
                }

                var validationResult = HoopsEntryUpdateValidator.ValidateUpdate(pool, existingEntry, entry, isAdmin);
                if (!validationResult.IsValid)
                {
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteStringAsync(validationResult.ErrorMessage);
                    return badReq;
                }

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
                    entry.AuditLog = existingEntry.AuditLog ?? new List<string>();
                    var changes = new List<string>();

                    if (!string.Equals(existingEntry.PlayerName, entry.PlayerName, StringComparison.Ordinal))
                        changes.Add($"Changed Bracket Name: {existingEntry.PlayerName} -> {entry.PlayerName}");

                    var allGames = await _gameRepo.GetGamesAsync(pool.SeasonId);
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

                var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                bool assignToMe = string.Equals(queryParams["assignToMe"], "true", StringComparison.OrdinalIgnoreCase);

                if (!isAdmin || assignToMe)
                    entry.UserId = principal.UserId;

                if (string.IsNullOrEmpty(entry.Id)) entry.Id = Guid.NewGuid().ToString();

                // Force correct type and season
                entry.Type = Constants.DocumentTypes.HoopsBracketEntry;
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
                var principal = SecurityHelper.ParseSwaHeader(req);
                if (principal == null)
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
                var principal = SecurityHelper.ParseSwaHeader(req);
                if (principal == null)
                {
                    var unauth = req.CreateResponse(HttpStatusCode.Unauthorized);
                    await unauth.WriteStringAsync("User is not authenticated.");
                    return unauth;
                }

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
