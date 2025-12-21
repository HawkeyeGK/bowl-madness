using Microsoft.Extensions.Logging;
using BowlPoolManager.Core.Dtos;
using Newtonsoft.Json; 

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
            var json = await GetRawPostseasonGamesJsonAsync(year);
            if (string.IsNullOrEmpty(json)) return new List<CfbdGameDto>();

            try 
            {
                return JsonConvert.DeserializeObject<List<CfbdGameDto>>(json) ?? new List<CfbdGameDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Deserialization failed.");
                return new List<CfbdGameDto>();
            }
        }

        public async Task<string> GetRawPostseasonGamesJsonAsync(int year)
        {
            EnsureAuth();
            try
            {
                var url = $"/games?year={year}&seasonType=postseason";
                return await _httpClient.GetStringAsync(url);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Raw fetch failed.");
                return $"Error fetching data: {ex.Message}";
            }
        }

        public async Task<List<CfbdGameDto>> GetScoreboardGamesAsync()
        {
            // Re-use the raw fetch to ensure consistency
            var json = await GetRawScoreboardJsonAsync();
            if (string.IsNullOrEmpty(json)) return new List<CfbdGameDto>();

            try
            {
                return JsonConvert.DeserializeObject<List<CfbdGameDto>>(json) ?? new List<CfbdGameDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scoreboard deserialization failed.");
                return new List<CfbdGameDto>();
            }
        }

        // NEW: Raw Fetch Implementation
        public async Task<string> GetRawScoreboardJsonAsync()
        {
            EnsureAuth();
            try
            {
                // classification=fbs is usually safer to reduce noise
                var url = "/scoreboard?classification=fbs"; 
                return await _httpClient.GetStringAsync(url);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scoreboard fetch failed.");
                return $"Error fetching scoreboard: {ex.Message}";
            }
        }

        private void EnsureAuth()
        {
            var apiKey = Environment.GetEnvironmentVariable("CfbdApiKey");
            if (!string.IsNullOrEmpty(apiKey) && !_httpClient.DefaultRequestHeaders.Contains("Authorization"))
            {
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            }
        }
    }
}
