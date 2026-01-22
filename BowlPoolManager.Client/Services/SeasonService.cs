using System.Net.Http.Json;
using BowlPoolManager.Core.Domain;

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

        public async Task UpsertSeasonAsync(Season season)
        {
            try
            {
                await _http.PostAsJsonAsync("api/Seasons", season);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving season: {ex.Message}");
                throw;
            }
        }
    }
}
