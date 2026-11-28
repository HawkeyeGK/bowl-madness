using System.Net.Http.Json;
using BowlPoolManager.Client.Helpers;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Helpers;

namespace BowlPoolManager.Client.Services
{
    public class SeasonService : ISeasonService
    {
        private readonly HttpClient _http;

        public SeasonService(HttpClient http)
        {
            _http = http;
        }

        public async Task<List<Season>> GetSeasonsAsync()
        {
            try
            {
                var seasons = await _http.GetFromJsonAsync<List<Season>>("api/Seasons");
                return seasons ?? new List<Season>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting seasons: {ex.Message}");
                return new List<Season>();
            }
        }

        // The season marked current, else the newest by year (CurrentSeason.Pick; issue 138).
        public async Task<Season?> GetCurrentSeasonAsync()
        {
            return CurrentSeason.Pick(await GetSeasonsAsync());
        }

        // Throws HttpRequestException carrying the server's message when the save is refused (issue 138).
        public async Task UpsertSeasonAsync(Season season)
        {
            var response = await _http.PostAsJsonAsync("api/Seasons", season);
            await ApiErrorText.EnsureSuccessAsync(response);
        }
    }
}
