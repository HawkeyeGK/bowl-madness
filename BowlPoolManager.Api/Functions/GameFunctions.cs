using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using BowlPoolManager.Api.Services;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core;
using System.Text.Json;
using System.Text;

namespace BowlPoolManager.Api.Functions
{
    public class GameFunctions
    {
        private readonly ILogger _logger;
        private readonly ICosmosDbService _cosmosService;

        public GameFunctions(ILoggerFactory loggerFactory, ICosmosDbService cosmosService)
        {
            _logger = loggerFactory.CreateLogger<GameFunctions>();
            _cosmosService = cosmosService;
        }

        [Function("GetGames")]
        public async Task<HttpResponseData> GetGames([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            _logger.LogInformation("Getting all games.");
            var games = await _cosmosService.GetGamesAsync();
            
            // Optional: Sort by date
            var sortedGames = games.OrderBy(g => g.StartTime).ToList();

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(sortedGames);
            return response;
        }

        [Function("SaveGame")]
        public async Task<HttpResponseData> SaveGame([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            _logger.LogInformation("Saving a game.");

            try
            {
                // 1. Authenticate
                var principal = ParseSwaHeader(req);
                if (principal == null || string.IsNullOrEmpty(principal.UserId))
                {
                    return req.CreateResponse(HttpStatusCode.Unauthorized);
                }

                // 2. Authorize (SuperAdmin Only)
                var userProfile = await _cosmosService.GetUserAsync(principal.UserId);
                if (userProfile == null || userProfile.AppRole != Constants.Roles.SuperAdmin)
                {
                    _logger.LogWarning($"User {principal.UserId} attempted to save a game without SuperAdmin rights.");
                    return req.CreateResponse(HttpStatusCode.Forbidden);
                }

                // 3. Process
                var game = await JsonSerializer.DeserializeAsync<BowlGame>(req.Body);
                if (game == null)
                {
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteStringAsync("Invalid game data.");
                    return badReq;
                }

                // We use Update (Upsert) to handle both Create and Edit scenarios
                await _cosmosService.UpdateGameAsync(game);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(game);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SaveGame failed.");
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }

        private ClientPrincipal? ParseSwaHeader(HttpRequestData req)
        {
            try
            {
                if (!req.Headers.TryGetValues("x-ms-client-principal", out var headerValues)) return null;
                var header = headerValues.FirstOrDefault();
                if (string.IsNullOrEmpty(header)) return null;

                var data = Convert.FromBase64String(header);
                var json = Encoding.UTF8.GetString(data);
                return JsonSerializer.Deserialize<ClientPrincipal>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                return null;
            }
        }
    }
}
