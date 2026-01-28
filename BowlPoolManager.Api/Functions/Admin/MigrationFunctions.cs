using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using BowlPoolManager.Core.Dtos;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Api.Repositories;
using BowlPoolManager.Core;
using BowlPoolManager.Api.Helpers;

namespace BowlPoolManager.Api.Functions.Admin
{
    public class MigrationFunctions
    {
        private readonly ILogger<MigrationFunctions> _logger;
        private readonly IMigrationRepository _migrationRepository; // Changed
        private readonly IEntryRepository _entryRepository;
        private readonly IUserRepository _userRepository;

        public MigrationFunctions(ILogger<MigrationFunctions> logger, IMigrationRepository migrationRepository, IEntryRepository entryRepository, IUserRepository userRepository)
        {
            _logger = logger;
            _migrationRepository = migrationRepository; // Changed
            _entryRepository = entryRepository;
            _userRepository = userRepository;
        }

        [Function("MigrationPing")]
        public IActionResult MigrationPing([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "admin/migration/ping")] HttpRequest req)
        {
            return new OkObjectResult("Pong");
        }

        [Function("AnalyzeLegacyData")]
        public async Task<IActionResult> AnalyzeLegacyData([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "admin/migration/analyze")] HttpRequest req)
        {
            if (!await IsSuperAdminAsync(req)) return new StatusCodeResult(StatusCodes.Status403Forbidden);

            _logger.LogInformation("Analyzing legacy data...");

            try
            {
                var (games, teamNames, entryCount) = await _migrationRepository.AnalyzeLegacyDataAsync();

                return new OkObjectResult(new MigrationAnalysisResult
                {
                    LegacyGames = games.OrderBy(g => g.StartTime).ToList(),
                    LegacyTeamNames = teamNames,
                    LegacyEntryCount = entryCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing legacy data");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }

        [Function("ExecuteMigration")]
        public async Task<IActionResult> ExecuteMigration([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "admin/migration/execute")] HttpRequest req)
        {
            if (!await IsSuperAdminAsync(req)) return new StatusCodeResult(StatusCodes.Status403Forbidden);

            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var migrationRequest = JsonConvert.DeserializeObject<MigrationExecutionRequest>(requestBody);

                if (migrationRequest == null) return new BadRequestObjectResult("Invalid request");

                var legacyEntries = await _migrationRepository.GetLegacyEntriesAsync();

                int migratedCount = 0;
                int errorCount = 0;

                foreach (var item in legacyEntries)
                {
                    try
                    {
                        // Create new entry
                        var newEntry = new BracketEntry
                        {
                            PoolId = migrationRequest.TargetPoolId,
                            SeasonId = migrationRequest.TargetSeasonId,
                            UserId = item.userId,
                            PlayerName = item.playerName,
                            TieBreakerPoints = (int?)item.tieBreakerPoints ?? 0,
                            CreatedOn = (DateTime?)item.createdOn ?? DateTime.UtcNow,
                            IsPaid = false,
                            Type = Constants.DocumentTypes.BracketEntry,
                            Picks = new Dictionary<string, string>()
                        };
                        
                        // Map Picks
                        if (item.picks != null)
                        {
                            var picksString = item.picks.ToString();
                            var oldPicks = JsonConvert.DeserializeObject<Dictionary<string, string>>(picksString);
                            
                            if (oldPicks != null)
                            {
                                foreach (var pick in oldPicks)
                                {
                                    string oldGameId = pick.Key;
                                    string oldTeamName = pick.Value;

                                    if (migrationRequest.GameMapping.TryGetValue(oldGameId, out var newGameId) &&
                                        !string.IsNullOrEmpty(newGameId))
                                    {
                                        string newTeamName = oldTeamName; 
                                        if (migrationRequest.TeamMapping.TryGetValue(oldTeamName, out var mappedTeamName) && 
                                            !string.IsNullOrEmpty(mappedTeamName))
                                        {
                                            newTeamName = mappedTeamName;
                                        }
                                        
                                        if (!string.IsNullOrEmpty(newGameId))
                                        {
                                            newEntry.Picks[newGameId] = newTeamName;
                                        }
                                    }
                                }
                            }
                        }

                        await _entryRepository.AddEntryAsync(newEntry);
                        migratedCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to migrate entry {item.id}");
                        errorCount++;
                    }
                }

                return new OkObjectResult(new { MigratedCount = migratedCount, ErrorCount = errorCount });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing migration");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }

        private async Task<bool> IsSuperAdminAsync(HttpRequest req)
        {
            try 
            {
                if (!req.Headers.TryGetValue("x-ms-client-principal", out var headerValues)) return false;
                var header = headerValues.FirstOrDefault();
                if (string.IsNullOrEmpty(header)) return false;

                var data = Convert.FromBase64String(header);
                var decoded = System.Text.Encoding.UTF8.GetString(data);
                
                var principal = JsonConvert.DeserializeObject<SecurityHelper.ClientPrincipal>(decoded);
                
                if (principal == null || string.IsNullOrEmpty(principal.UserId)) return false;

                var user = await _userRepository.GetUserAsync(principal.UserId);
                return user != null && user.AppRole == Constants.Roles.SuperAdmin;
            }
            catch
            {
                return false;
            }
        }
    }
}
