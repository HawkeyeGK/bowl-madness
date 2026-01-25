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
                        // FIX: Convert JToken to Dictionary. 
                        // System.Text.Json (used in GameFunctions) cannot serialize JTokens,
                        // but it CAN serialize standard Dictionaries.
                        HomeRaw = g["homeTeam"]?.ToObject<Dictionary<string, object>>(),
                        HomePointsRoot = (int?)g["homePoints"],
                        
                        AwayRaw = g["awayTeam"]?.ToObject<Dictionary<string, object>>(),
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

        public async Task<List<BowlPoolManager.Core.Domain.TeamInfo>> GetFbsTeamsAsync()
        {
            try
            {
                var json = await ExecuteRequestAsync("/teams/fbs");
                if (string.IsNullOrEmpty(json) || json.StartsWith("Error")) return new List<BowlPoolManager.Core.Domain.TeamInfo>();

                var rawTeams = JsonConvert.DeserializeObject<List<RawTeamDto>>(json);
                if (rawTeams == null) return new List<BowlPoolManager.Core.Domain.TeamInfo>();

                return rawTeams.Select(r => new BowlPoolManager.Core.Domain.TeamInfo
                {
                    SchoolId = r.Id,
                    School = r.School ?? string.Empty,
                    Mascot = r.Mascot ?? string.Empty,
                    Abbreviation = r.Abbreviation ?? string.Empty,
                    Conference = r.Conference ?? string.Empty,
                    Color = r.Color ?? string.Empty,
                    AltColor = r.AlternateColor ?? string.Empty,
                    Logos = r.Logos ?? new List<string>()
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch/deserialize FBS teams.");
                return new List<BowlPoolManager.Core.Domain.TeamInfo>();
            }
        }

        // Private DTO to match CFBD API structure
        private class RawTeamDto
        {
            [JsonProperty("id")]
            public int Id { get; set; }
            [JsonProperty("school")]
            public string? School { get; set; }
            [JsonProperty("mascot")]
            public string? Mascot { get; set; }
            [JsonProperty("abbreviation")]
            public string? Abbreviation { get; set; }
            [JsonProperty("conference")]
            public string? Conference { get; set; }
            [JsonProperty("color")]
            public string? Color { get; set; }
            [JsonProperty("alternateColor")]
            public string? AlternateColor { get; set; }
            [JsonProperty("logos")]
            public List<string>? Logos { get; set; }
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
