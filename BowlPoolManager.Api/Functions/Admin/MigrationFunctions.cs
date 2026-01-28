using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Cosmos;
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
        private readonly CosmosClient _cosmosClient;
        private readonly IEntryRepository _entryRepository;
        private readonly IUserRepository _userRepository;
        private const string LegacyContainerName = "MainContainer";

        public MigrationFunctions(ILogger<MigrationFunctions> logger, CosmosClient cosmosClient, IEntryRepository entryRepository, IUserRepository userRepository)
        {
            _logger = logger;
            _cosmosClient = cosmosClient;
            _entryRepository = entryRepository;
            _userRepository = userRepository;
        }

        [Function("AnalyzeLegacyData")]
        public async Task<IActionResult> AnalyzeLegacyData([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "admin/migration/analyze")] HttpRequest req)
        {
            if (!await IsSuperAdminAsync(req)) return new StatusCodeResult(StatusCodes.Status403Forbidden);

            _logger.LogInformation("Analyzing legacy data...");

            try
            {
                // Access MainContainer directly
                var container = _cosmosClient.GetContainer(Constants.Database.DbName, LegacyContainerName);

                // 1. Fetch Games
                var games = new List<LegacyGameDto>();
                var gameIterator = container.GetItemQueryIterator<dynamic>(new QueryDefinition("SELECT * FROM c WHERE c.type = 'BowlGame'"));

                while (gameIterator.HasMoreResults)
                {
                    foreach (var item in await gameIterator.ReadNextAsync())
                    {
                        games.Add(new LegacyGameDto
                        {
                            Id = item.id,
                            Description = $"{item.bowlName}: {item.teamHome} vs {item.teamAway}",
                            HomeTeam = item.teamHome,
                            AwayTeam = item.teamAway,
                            StartTime = item.startTime
                        });
                    }
                }

                // 2. Fetch Entries to find Team Names
                var teamNames = new HashSet<string>();
                int entryCount = 0;
                var entryIterator = container.GetItemQueryIterator<dynamic>(new QueryDefinition("SELECT * FROM c WHERE c.type = 'BracketEntry'"));

                while (entryIterator.HasMoreResults)
                {
                    foreach (var item in await entryIterator.ReadNextAsync())
                    {
                        entryCount++;
                        if (item.picks != null)
                        {
                            try
                            {
                                var picksString = item.picks.ToString();
                                var picks = JsonConvert.DeserializeObject<Dictionary<string, string>>(picksString);
                                if (picks != null)
                                {
                                    foreach (var team in picks.Values)
                                    {
                                        if (!string.IsNullOrWhiteSpace(team))
                                        {
                                            teamNames.Add(team);
                                        }
                                    }
                                }
                            }
                            catch
                            {
                                // Ignore parsing errors for individual picks
                            }
                        }
                    }
                }

                return new OkObjectResult(new MigrationAnalysisResult
                {
                    LegacyGames = games.OrderBy(g => g.StartTime).ToList(),
                    LegacyTeamNames = teamNames.OrderBy(t => t).ToList(),
                    LegacyEntryCount = entryCount
                });
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                 return new BadRequestObjectResult($"Container '{LegacyContainerName}' not found in database '{Constants.Database.DbName}'.");
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

                var container = _cosmosClient.GetContainer(Constants.Database.DbName, LegacyContainerName);
                var query = new QueryDefinition("SELECT * FROM c WHERE c.type = 'BracketEntry'");
                
                var iterator = container.GetItemQueryIterator<dynamic>(query);

                int migratedCount = 0;
                int errorCount = 0;

                while (iterator.HasMoreResults)
                {
                    foreach (var item in await iterator.ReadNextAsync())
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
                
                // Using dynamic to avoid needing SecurityHelper.ClientPrincipal access if strict, 
                // but we know SecurityHelper is available in Api.Helpers.
                // However, deserialization to a local or mapped type is safer.
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
