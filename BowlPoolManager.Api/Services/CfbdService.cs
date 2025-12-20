using System.Net.Http.Json;
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
            // Retrieve key from Environment Variables (set in local.settings.json or Azure Config)
            var apiKey = Environment.GetEnvironmentVariable("CfbdApiKey");
            
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogError("CfbdApiKey is missing from configuration.");
                return new List<CfbdGameDto>();
            }

            // Add Authorization Header if not present
            if (!_httpClient.DefaultRequestHeaders.Contains("Authorization"))
            {
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            }

            try
            {
                // Fetch postseason games
                var url = $"/games?year={year}&seasonType=postseason";
                var games = await _httpClient.GetFromJsonAsync<List<CfbdGameDto>>(url);
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
