using System.Net.Http.Json;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Dtos;

namespace BowlPoolManager.Client.Services
{
    public class HoopsGameService : IHoopsGameService
    {
        private readonly HttpClient _http;

        public HoopsGameService(HttpClient http)
        {
            _http = http;
        }

        public async Task<List<HoopsGame>> GetGamesAsync(string poolId)
        {
            try
            {
                var result = await _http.GetFromJsonAsync<List<HoopsGame>>($"api/GetHoopsGames?poolId={poolId}");
                return result ?? new();
            }
            catch
            {
                return new();
            }
        }

        public async Task<List<HoopsGame>?> GenerateBracketAsync(BracketGenerationRequest request)
        {
            try
            {
                var resp = await _http.PostAsJsonAsync("api/GenerateBracket", request);
                if (!resp.IsSuccessStatusCode) return null;
                return await resp.Content.ReadFromJsonAsync<List<HoopsGame>>();
            }
            catch
            {
                return null;
            }
        }

        public async Task<HoopsGame?> UpdateGameAsync(HoopsGame game)
        {
            try
            {
                var resp = await _http.PutAsJsonAsync("api/UpdateHoopsGame", game);
                if (!resp.IsSuccessStatusCode) return null;
                return await resp.Content.ReadFromJsonAsync<HoopsGame>();
            }
            catch
            {
                return null;
            }
        }

        public async Task<bool> SaveTeamAssignmentsAsync(List<HoopsGame> games)
        {
            try
            {
                var resp = await _http.PostAsJsonAsync("api/SaveHoopsTeamAssignments", games);
                return resp.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<HoopsGame?> SaveGameAsync(HoopsGame game)
        {
            try
            {
                var resp = await _http.PostAsJsonAsync("api/SaveHoopsGame", game);
                if (!resp.IsSuccessStatusCode) return null;
                return await resp.Content.ReadFromJsonAsync<HoopsGame>();
            }
            catch
            {
                return null;
            }
        }

        public async Task<bool> ForcePropagationAsync(string seasonId)
        {
            try
            {
                var resp = await _http.PostAsync($"api/ForceHoopsPropagation?seasonId={seasonId}", null);
                return resp.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}
