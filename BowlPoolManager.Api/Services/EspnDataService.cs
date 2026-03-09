using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Dtos;

namespace BowlPoolManager.Api.Services
{
    public class EspnDataService : IEspnDataService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<EspnDataService> _logger;
        private const string TeamsUrl = "https://site.api.espn.com/apis/site/v2/sports/basketball/mens-college-basketball/teams?limit=1000";
        private const string SearchUrl = "https://site.api.espn.com/apis/search/v2";
        private const string ScoreboardUrl = "https://site.api.espn.com/apis/site/v2/sports/basketball/mens-college-basketball/scoreboard?limit=100";

        public EspnDataService(HttpClient httpClient, ILogger<EspnDataService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<List<TeamInfo>> GetTeamsAsync()
        {
            try
            {
                var json = await _httpClient.GetStringAsync(TeamsUrl);
                var root = JObject.Parse(json);

                var teamTokens = root["sports"]?[0]?["leagues"]?[0]?["teams"];
                if (teamTokens == null)
                {
                    _logger.LogWarning("ESPN API response did not contain expected teams path.");
                    return new List<TeamInfo>();
                }

                var results = new List<TeamInfo>();
                foreach (var entry in teamTokens)
                {
                    var team = entry["team"];
                    if (team == null) continue;

                    var logos = team["logos"]?.Select(l => l["href"]?.ToString())
                        .Where(h => !string.IsNullOrEmpty(h))
                        .Cast<string>()
                        .ToList() ?? new List<string>();

                    results.Add(new TeamInfo
                    {
                        SchoolId = int.TryParse(team["id"]?.ToString(), out var id) ? id : 0,
                        School = team["location"]?.ToString() ?? string.Empty,
                        Mascot = team["name"]?.ToString() ?? string.Empty,
                        Abbreviation = team["abbreviation"]?.ToString() ?? string.Empty,
                        Color = team["color"]?.ToString() ?? string.Empty,
                        AltColor = team["alternateColor"]?.ToString() ?? string.Empty,
                        Logos = logos
                    });
                }

                _logger.LogInformation("ESPN API returned {Count} teams.", results.Count);
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch/parse ESPN teams.");
                return new List<TeamInfo>();
            }
        }

        public async Task<List<TeamInfo>> SearchTeamsAsync(string query)
        {
            try
            {
                var url = $"{SearchUrl}?query={Uri.EscapeDataString(query)}&sport=basketball&limit=20";
                var json = await _httpClient.GetStringAsync(url);
                var root = JObject.Parse(json);

                var resultGroups = root["results"];
                if (resultGroups == null) return new List<TeamInfo>();

                var teams = new List<TeamInfo>();
                foreach (var group in resultGroups)
                {
                    var items = group["contents"];
                    if (items == null) continue;

                    foreach (var item in items)
                    {
                        if (item["type"]?.ToString() != "team") continue;
                        if (item["subtitle"]?.ToString() != "NCAAM") continue;

                        // uid format: "s:40~l:41~t:2598" — extract numeric team ID after "~t:"
                        var uid = item["uid"]?.ToString() ?? string.Empty;
                        var tIdx = uid.IndexOf("~t:", StringComparison.Ordinal);
                        if (!int.TryParse(tIdx >= 0 ? uid[(tIdx + 3)..] : string.Empty, out var schoolId) || schoolId == 0)
                            continue;

                        teams.Add(new TeamInfo
                        {
                            SchoolId = schoolId,
                            School = item["displayName"]?.ToString() ?? string.Empty,
                            PrimaryLogoUrl = item["image"]?["default"]?.ToString() ?? string.Empty
                        });
                    }
                }

                _logger.LogInformation("ESPN search for '{Query}' returned {Count} NCAAM teams.", query, teams.Count);
                return teams;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to search ESPN teams for query '{Query}'.", query);
                return new List<TeamInfo>();
            }
        }

        public async Task<List<EspnScoreboardGameDto>> GetScoreboardGamesAsync()
        {
            try
            {
                var json = await _httpClient.GetStringAsync(ScoreboardUrl);
                var root = JObject.Parse(json);

                var events = root["events"];
                if (events == null)
                {
                    _logger.LogWarning("ESPN scoreboard response contained no events.");
                    return new List<EspnScoreboardGameDto>();
                }

                var results = new List<EspnScoreboardGameDto>();

                foreach (var evt in events)
                {
                    var competition = evt["competitions"]?.FirstOrDefault();
                    if (competition == null) continue;

                    var competitors = competition["competitors"];
                    if (competitors == null) continue;

                    string? homeTeam = null, awayTeam = null;
                    int? homeId = null, awayId = null;
                    int? homePoints = null, awayPoints = null;

                    foreach (var competitor in competitors)
                    {
                        var homeAway = competitor["homeAway"]?.ToString();
                        var teamName = competitor["team"]?["displayName"]?.ToString();
                        int.TryParse(competitor["team"]?["id"]?.ToString(), out var teamId);
                        int.TryParse(competitor["score"]?.ToString(), out var score);

                        if (string.Equals(homeAway, "home", StringComparison.OrdinalIgnoreCase))
                        {
                            homeTeam = teamName;
                            homeId = teamId > 0 ? teamId : null;
                            homePoints = score > 0 ? score : null;
                        }
                        else
                        {
                            awayTeam = teamName;
                            awayId = teamId > 0 ? teamId : null;
                            awayPoints = score > 0 ? score : null;
                        }
                    }

                    var statusType = competition["status"]?["type"];
                    var statusName = statusType?["name"]?.ToString();
                    var completed = (bool?)statusType?["completed"] ?? false;
                    var period = (int?)competition["status"]?["period"];
                    var displayClock = competition["status"]?["displayClock"]?.ToString();

                    results.Add(new EspnScoreboardGameDto
                    {
                        Id = evt["id"]?.ToString() ?? string.Empty,
                        HomeTeam = homeTeam,
                        AwayTeam = awayTeam,
                        HomeId = homeId,
                        AwayId = awayId,
                        HomePoints = homePoints,
                        AwayPoints = awayPoints,
                        Completed = completed,
                        StatusName = statusName,
                        Period = period,
                        DisplayClock = displayClock
                    });
                }

                _logger.LogInformation("ESPN scoreboard returned {Count} games.", results.Count);
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch/parse ESPN scoreboard.");
                return new List<EspnScoreboardGameDto>();
            }
        }
    }
}
