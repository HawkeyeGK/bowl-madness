using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using BowlPoolManager.Core;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Api.Helpers;
using BowlPoolManager.Api.Repositories;

namespace BowlPoolManager.Api.Functions
{
    public class BackupFunctions
    {
        private readonly ILogger _logger;
        private readonly IEntryRepository _entryRepo;
        private readonly IGameRepository _gameRepo;
        private readonly IUserRepository _userRepo;

        public BackupFunctions(ILoggerFactory loggerFactory, IEntryRepository entryRepo, IGameRepository gameRepo, IUserRepository userRepo)
        {
            _logger = loggerFactory.CreateLogger<BackupFunctions>();
            _entryRepo = entryRepo;
            _gameRepo = gameRepo;
            _userRepo = userRepo;
        }

        [Function("GetBackupData")]
        public async Task<HttpResponseData> GetBackupData([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            _logger.LogInformation("Generating backup download.");

            try
            {
                // 1. Auth Check (SuperAdmin only)
                var authResult = await SecurityHelper.ValidateSuperAdminAsync(req, _userRepo);
                if (!authResult.IsValid) 
                {
                    _logger.LogWarning("Backup attempt failed validation.");
                    return authResult.ErrorResponse!;
                }

                // 2. Fetch Data
                var allEntries = await _entryRepo.GetEntriesAsync();
                var allGames = await _gameRepo.GetGamesAsync();

                // 3. Create Game ID Lookup Map
                var gameMap = allGames.ToDictionary(g => g.Id, g => g.BowlName);

                // 4. Transform Entries (Replace keys)
                var backupData = allEntries.Select(entry => new
                {
                    entry.Id,
                    entry.PoolId,
                    entry.UserId,
                    entry.PlayerName,
                    entry.TieBreakerPoints,
                    entry.CreatedOn,
                    // Replace GameId keys with BowlName
                    Picks = entry.Picks?.ToDictionary(
                        kvp => gameMap.ContainsKey(kvp.Key) ? gameMap[kvp.Key] : kvp.Key, // Fallback to ID if not found
                        kvp => kvp.Value
                    )
                });

                // 5. Serialize & Return
                var jsonString = JsonSerializer.Serialize(backupData, new JsonSerializerOptions { WriteIndented = true });
                
                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/json");
                response.Headers.Add("Content-Disposition", $"attachment; filename=\"BowlPool_Backup_{DateTime.UtcNow:yyyy-MM-dd}.json\"");

                await response.WriteStringAsync(jsonString);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetBackupData failed.");
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }
    }
}
