using System.Net.Http.Json;
using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Client.Services
{
    public class ConfigurationService : IConfigurationService
    {
        private readonly HttpClient _http;
        private List<TeamInfo>? _cachedTeams;
        private List<TeamInfo>? _cachedBasketballTeams;

        public ConfigurationService(HttpClient http)
        {
            _http = http;
        }

        public async Task<List<TeamInfo>> GetTeamsAsync()
        {
            if (_cachedTeams != null && _cachedTeams.Any())
                return _cachedTeams;

            try
            {
                var config = await _http.GetFromJsonAsync<TeamConfig>("api/GetTeamConfig");
                if (config != null)
                    _cachedTeams = config.Teams;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading teams: {ex.Message}");
            }

            return _cachedTeams ?? new List<TeamInfo>();
        }

        public async Task<List<TeamInfo>> GetBasketballTeamsAsync()
        {
            if (_cachedBasketballTeams != null && _cachedBasketballTeams.Any())
                return _cachedBasketballTeams;

            try
            {
                var config = await _http.GetFromJsonAsync<TeamConfig>("api/GetBasketballTeamConfig");
                if (config != null)
                    _cachedBasketballTeams = config.Teams.Where(t => !string.IsNullOrEmpty(t.PrimaryLogoUrl)).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading basketball teams: {ex.Message}");
            }

            return _cachedBasketballTeams ?? new List<TeamInfo>();
        }
    }
}
