using Microsoft.Extensions.Logging;
using BowlPoolManager.Core.Dtos;
using Newtonsoft.Json; 
using System.Net.Http.Headers;

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
            return await ExecuteRequestAsync($"/games?year={year}&seasonType=postseason");
        }

        public async Task<List<CfbdGameDto>> GetScoreboardGamesAsync()
        {
            var json = await GetRawScoreboardJsonAsync();
            if (string.IsNullOrEmpty(json)) return new List<CfbdGameDto>();

            try
            {
                // Manual mapping to ensure Notes and other fields are correct
                // because sometimes automatic deserialization misses fields or casing differs.
                var token = Newtonsoft.Json.Linq.JToken.Parse(json);
                if (token is Newtonsoft.Json.Linq.JArray arr)
                {
                    return arr.Select(g => new CfbdGameDto
                    {
                        Id = (int?)g["id"] ?? 0,
                        Completed = (bool?)g["completed"] ?? false,
                        StatusRaw = (string?)g["status"],
                        Period = (int?)g["period"],
                        Clock = (string?)g["clock"],
                        // Pass raw objects for the Smart Wrappers to handle
                        HomeRaw = g["homeTeam"],
                        HomePointsRoot = (int?)g["homePoints"],
                        AwayRaw = g["awayTeam"],
                        AwayPointsRoot = (int?)g["awayPoints"],
                        // Explicitly map Notes
                        Notes = (string?)g["notes"]
                    }).ToList();
                }

                // If not an array (e.g. error object or single object), try single or fail
                return new List<CfbdGameDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scoreboard deserialization failed.");
                return new List<CfbdGameDto>();
            }
        }

        public async Task<string> GetRawScoreboardJsonAsync()
        {
            return await ExecuteRequestAsync("/scoreboard?classification=fbs");
        }

        // --- HELPER: Stateless Request Execution ---
        // This ensures the API Key is attached to the specific MESSAGE, not the shared Client.
        private async Task<string> ExecuteRequestAsync(string relativeUrl)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, relativeUrl);
                
                var apiKey = Environment.GetEnvironmentVariable("CfbdApiKey");
                
                if (string.IsNullOrEmpty(apiKey))
                {
                    _logger.LogError("CRITICAL: 'CfbdApiKey' is missing from Environment Variables!");
                    return "Error: API Key Missing on Server";
                }

                // Explicitly attach header to THIS specific request message
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                var response = await _httpClient.SendAsync(request);
                
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"CFBD API Error ({response.StatusCode}) for {relativeUrl}: {error}");
                    return $"Error: {response.StatusCode} - {error}";
                }

                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Request execution failed for {relativeUrl}");
                return $"Exception: {ex.Message}";
            }
        }
    }
}
