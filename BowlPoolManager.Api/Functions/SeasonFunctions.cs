using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using BowlPoolManager.Api.Repositories;
using BowlPoolManager.Core.Domain;
using System.Text.Json;

namespace BowlPoolManager.Api.Functions
{
    public class SeasonFunctions
    {
        private readonly ISeasonRepository _seasonRepository;
        private readonly ILogger<SeasonFunctions> _logger;

        public SeasonFunctions(ISeasonRepository seasonRepository, ILogger<SeasonFunctions> logger)
        {
            _seasonRepository = seasonRepository;
            _logger = logger;
        }

        [Function("GetSeasons")]
        public async Task<IActionResult> GetSeasons([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "Seasons")] HttpRequest req)
        {
            _logger.LogInformation("Getting all seasons.");
            var seasons = await _seasonRepository.GetSeasonsAsync();
            return new OkObjectResult(seasons);
        }

        [Function("SaveSeason")]
        public async Task<IActionResult> SaveSeason([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "Seasons")] HttpRequest req)
        {
            _logger.LogInformation("Saving season.");
            
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var season = JsonSerializer.Deserialize<Season>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (season == null || string.IsNullOrEmpty(season.Id))
            {
                return new BadRequestObjectResult("Invalid season data.");
            }

            // Ensure type is set correctly
            season.Type = "Season";

            await _seasonRepository.UpsertSeasonAsync(season);
            return new OkResult();
        }
    }
}
