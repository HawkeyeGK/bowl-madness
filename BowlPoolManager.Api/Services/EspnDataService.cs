using Microsoft.Extensions.Logging;
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
        private const string ScoreboardUrl = "https://site.api.espn.com/apis/site/v2/sports/basketball/mens-college-basketball/scoreboard";

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

        public async Task<List<BasketballGameDto>> GetBasketballScoreboardAsync()
        {
            try
            {
                var json = await _httpClient.GetStringAsync(ScoreboardUrl);
                var root = JObject.Parse(json);
                var events = root["events"] as JArray;
                if (events == null) return new List<BasketballGameDto>();

                var result = new List<BasketballGameDto>();
                foreach (var ev in events)
                {
                    var competition = ev["competitions"]?[0];
                    if (competition == null) continue;

                    var competitors = competition["competitors"] as JArray;
                    if (competitors == null) continue;

                    var home = competitors.FirstOrDefault(c => c["homeAway"]?.ToString() == "home");
                    var away = competitors.FirstOrDefault(c => c["homeAway"]?.ToString() == "away");
                    if (home == null || away == null) continue;

                    var statusName = ev["status"]?["type"]?["name"]?.ToString() ?? "";
                    var statusRaw = statusName switch
                    {
                        "STATUS_FINAL" => "final",
                        "STATUS_IN_PROGRESS" => "in_progress",
                        _ => "scheduled"
                    };

                    result.Add(new BasketballGameDto
                    {
                        Id = int.TryParse(ev["id"]?.ToString(), out var eid) ? eid : 0,
                        StatusRaw = statusRaw,
                        Completed = statusRaw == "final",
                        Period = (int?)ev["status"]?["period"],
                        Clock = ev["status"]?["displayClock"]?.ToString(),
                        HomeRaw = home["team"]?["location"]?.ToString(),
                        AwayRaw = away["team"]?["location"]?.ToString(),
                        HomePointsRoot = int.TryParse(home["score"]?.ToString(), out var hs) ? hs : (int?)null,
                        AwayPointsRoot = int.TryParse(away["score"]?.ToString(), out var aws) ? aws : (int?)null,
                    });
                }

                _logger.LogInformation("ESPN basketball scoreboard returned {Count} games.", result.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch ESPN basketball scoreboard.");
                return new List<BasketballGameDto>();
            }
        }
    }
}
