using System.Net.Http.Json;
using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Client.Services
{
    public class ConfigurationService : IConfigurationService
    {
        private readonly HttpClient _http;
        private List<TeamInfo>? _cachedTeams;

        public ConfigurationService(HttpClient http)
        {
            _http = http;
        }

        public async Task<List<TeamInfo>> GetTeamsAsync()
        {
            if (_cachedTeams != null && _cachedTeams.Any())
            {
                return _cachedTeams;
            }

            try
            {
                 // API returns TeamConfig object which contains the list
                var config = await _http.GetFromJsonAsync<TeamConfig>("api/GetTeamConfig");
                if (config != null)
                {
                    _cachedTeams = config.Teams;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading teams: {ex.Message}");
            }

            return _cachedTeams ?? new List<TeamInfo>();
        }
    }
}
