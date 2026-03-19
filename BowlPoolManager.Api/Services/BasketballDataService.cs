using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Dtos;

namespace BowlPoolManager.Api.Services
{
    public class BasketballDataService : IBasketballDataService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<BasketballDataService> _logger;
        private const string BaseUrl = "https://api.collegebasketballdata.com";

        public BasketballDataService(HttpClient httpClient, ILogger<BasketballDataService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _httpClient.BaseAddress = new Uri(BaseUrl);
        }

        public async Task<List<TeamInfo>> GetTeamsAsync()
        {
            try
            {
                var json = await ExecuteRequestAsync("/teams");
                if (string.IsNullOrEmpty(json) || json.StartsWith("Error"))
                    return new List<TeamInfo>();

                var rawTeams = JsonConvert.DeserializeObject<List<RawBasketballTeamDto>>(json);
                if (rawTeams == null) return new List<TeamInfo>();

                return rawTeams.Select(r => new TeamInfo
                {
                    SchoolId = r.Id,
                    School = r.School ?? string.Empty,
                    Mascot = r.Mascot ?? string.Empty,
                    Abbreviation = r.Abbreviation ?? string.Empty,
                    Conference = r.Conference ?? string.Empty,
                    Color = r.PrimaryColor ?? string.Empty,
                    AltColor = r.SecondaryColor ?? string.Empty,
                    Logos = r.Logos ?? new List<string>()
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch/deserialize basketball teams.");
                return new List<TeamInfo>();
            }
        }

        public async Task<List<BasketballGameDto>> GetScoreboardGamesAsync()
        {
            var json = await GetRawScoreboardJsonAsync();
            if (string.IsNullOrEmpty(json) || json.StartsWith("Error"))
                return new List<BasketballGameDto>();

            try
            {
                var token = JToken.Parse(json);
                if (token is JArray arr)
                {
                    return arr.Select(g => new BasketballGameDto
                    {
                        Id = (int?)g["id"] ?? 0,
                        Completed = (bool?)g["completed"] ?? false,
                        StatusRaw = (string?)g["status"],
                        Period = (int?)g["period"],
                        Clock = (string?)g["clock"],
                        HomeRaw = g["homeTeam"]?.ToObject<Dictionary<string, object>>(),
                        HomePointsRoot = (int?)g["homePoints"],
                        AwayRaw = g["awayTeam"]?.ToObject<Dictionary<string, object>>(),
                        AwayPointsRoot = (int?)g["awayPoints"],
                        Notes = (string?)g["notes"]
                    }).ToList();
                }

                return new List<BasketballGameDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Basketball scoreboard deserialization failed.");
                return new List<BasketballGameDto>();
            }
        }

        public async Task<string> GetRawScoreboardJsonAsync()
        {
            return await ExecuteRequestAsync("/scoreboard?classification=di");
        }

        public async Task<List<BasketballGameDto>> GetTournamentGamesAsync(int year)
        {
            var json = await GetRawTournamentGamesJsonAsync(year);
            if (string.IsNullOrEmpty(json) || json.StartsWith("Error"))
                return new List<BasketballGameDto>();

            try
            {
                var token = JToken.Parse(json);
                if (token is JArray arr)
                {
                    return arr.Select(g => new BasketballGameDto
                    {
                        Id = (int?)g["id"] ?? 0,
                        Completed = (bool?)g["completed"] ?? false,
                        StatusRaw = (string?)g["status"],
                        // /games endpoint returns team names as strings (homeTeam or home_team)
                        HomeRaw = (string?)g["homeTeam"] ?? (string?)g["home_team"],
                        AwayRaw = (string?)g["awayTeam"] ?? (string?)g["away_team"],
                        HomeIdRaw = (int?)g["homeId"] ?? (int?)g["home_id"],
                        AwayIdRaw = (int?)g["awayId"] ?? (int?)g["away_id"],
                        HomePointsRoot = (int?)g["homePoints"] ?? (int?)g["home_points"],
                        AwayPointsRoot = (int?)g["awayPoints"] ?? (int?)g["away_points"],
                        Notes = (string?)g["notes"]
                    }).ToList();
                }

                return new List<BasketballGameDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Basketball tournament games deserialization failed.");
                return new List<BasketballGameDto>();
            }
        }

        public async Task<string> GetRawTournamentGamesJsonAsync(int year)
        {
            return await ExecuteRequestAsync($"/games?year={year}&seasonType=postseason");
        }

        private async Task<string> ExecuteRequestAsync(string relativeUrl)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, relativeUrl);

                var apiKey = Environment.GetEnvironmentVariable("CfbdApiKeyHoops");

                if (string.IsNullOrEmpty(apiKey))
                {
                    _logger.LogError("CRITICAL: 'CfbdApiKeyHoops' is missing from Environment Variables!");
                    return "Error: API Key Missing on Server";
                }

                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Basketball API Error ({StatusCode}) for {Url}: {Error}", response.StatusCode, relativeUrl, error);
                    return $"Error: {response.StatusCode} - {error}";
                }

                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Basketball API request failed for {Url}", relativeUrl);
                return $"Exception: {ex.Message}";
            }
        }

        private class RawBasketballTeamDto
        {
            [JsonProperty("id")]
            public int Id { get; set; }

            [JsonProperty("school")]
            public string? School { get; set; }

            [JsonProperty("mascot")]
            public string? Mascot { get; set; }

            [JsonProperty("abbreviation")]
            public string? Abbreviation { get; set; }

            [JsonProperty("primaryColor")]
            public string? PrimaryColor { get; set; }

            [JsonProperty("secondaryColor")]
            public string? SecondaryColor { get; set; }

            [JsonProperty("conference")]
            public string? Conference { get; set; }

            [JsonProperty("logos")]
            public List<string>? Logos { get; set; }
        }
    }
}
