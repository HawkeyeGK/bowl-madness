using System.Net.Http.Json;
using System.Text.Json; // Required for JsonSerializerOptions
using Microsoft.Extensions.Logging;
using BowlPoolManager.Core.Dtos;

namespace BowlPoolManager.Api.Services
{
    public class CfbdService : ICfbdService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<CfbdService> _logger;
        private const string BaseUrl = "https://api.collegefootballdata.com";

        public CfbdService(HttpClient httpClient, ILogger<CfbdService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _httpClient.BaseAddress = new Uri(BaseUrl);
        }

        public async Task<List<CfbdGameDto>> GetPostseasonGamesAsync(int year)
        {
            var apiKey = Environment.GetEnvironmentVariable("CfbdApiKey");
            
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogError("CfbdApiKey is missing from configuration.");
                return new List<CfbdGameDto>();
            }

            if (!_httpClient.DefaultRequestHeaders.Contains("Authorization"))
            {
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            }

            try
            {
                var url = $"/games?year={year}&seasonType=postseason";
                
                // --- ROBUST SERIALIZATION SETTINGS ---
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, // Handles home_team -> HomeTeam
                    NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
                };
                
                // Debug: Log the attempt
                _logger.LogInformation($"Fetching CFBD Games for {year}...");

                var games = await _httpClient.GetFromJsonAsync<List<CfbdGameDto>>(url, options);
                
                // Debug: Log the result
                if (games != null && games.Any())
                {
                    var first = games.First();
                    _logger.LogInformation($"Fetched {games.Count} games. First Game: {first.Notes} | {first.HomeTeam} vs {first.AwayTeam}");
                }
                else
                {
                    _logger.LogWarning("Fetched games list was empty or null.");
                }

                return games ?? new List<CfbdGameDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch games from CFBD.");
                return new List<CfbdGameDto>();
            }
        }
    }
}
