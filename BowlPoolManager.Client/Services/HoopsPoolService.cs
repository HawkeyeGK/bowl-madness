using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Client.Services
{
    public class HoopsPoolService : IHoopsPoolService
    {
        private readonly HttpClient _http;

        public HoopsPoolService(HttpClient http)
        {
            _http = http;
        }

        public async Task<List<HoopsPool>> GetPoolsAsync(string? seasonId = null)
        {
            var url = string.IsNullOrEmpty(seasonId) ? "api/GetHoopsPools" : $"api/GetHoopsPools?seasonId={seasonId}";
            return await _http.GetFromJsonAsync<List<HoopsPool>>(url) ?? new List<HoopsPool>();
        }

        public async Task<HoopsPool?> GetPoolAsync(string poolId)
        {
            var pools = await GetPoolsAsync();
            return pools.FirstOrDefault(p => p.Id == poolId);
        }

        public async Task<HoopsPool?> CreatePoolAsync(HoopsPool pool)
        {
            var json = JsonSerializer.Serialize(pool);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _http.PostAsync("api/CreateHoopsPool", content);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<HoopsPool>();
        }

        public async Task<HoopsPool?> UpdatePoolAsync(HoopsPool pool)
        {
            var json = JsonSerializer.Serialize(pool);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _http.PutAsync("api/UpdateHoopsPool", content);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<HoopsPool>();
        }

        public async Task<bool> DeletePoolAsync(string poolId)
        {
            var response = await _http.DeleteAsync($"api/DeleteHoopsPool/{poolId}");
            return response.IsSuccessStatusCode;
        }

        public async Task<HoopsPool?> ToggleConclusionAsync(string poolId)
        {
            var response = await _http.PostAsync($"api/HoopsPools/{poolId}/ToggleConclusion", null);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<HoopsPool>();
        }
    }
}
