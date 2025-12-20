using Microsoft.Extensions.Logging;
using BowlPoolManager.Core.Dtos;
using Newtonsoft.Json; // Switching to Newtonsoft for robust handling

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
                
                _logger.LogInformation($"Fetching CFBD Games (Raw String) for {year}...");

                // 1. Fetch Raw String
                var json = await _httpClient.GetStringAsync(url);

                // 2. Debug Log (First 200 chars to verify format)
                if (!string.IsNullOrEmpty(json))
                {
                    var snippet = json.Length > 200 ? json.Substring(0, 200) : json;
                    _logger.LogInformation($"CFBD Response Snippet: {snippet}");
                }

                // 3. Deserialize using Newtonsoft
                // This uses the [JsonProperty("home_team")] attributes we added to the DTO
                var games = JsonConvert.DeserializeObject<List<CfbdGameDto>>(json);
                
                if (games != null && games.Any())
                {
                    var first = games.First();
                    _logger.LogInformation($"Parsed {games.Count} games. Sample: {first.Notes} | {first.HomeTeam} vs {first.AwayTeam}");
                }
                else
                {
                    _logger.LogWarning("Deserialized list was empty.");
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
