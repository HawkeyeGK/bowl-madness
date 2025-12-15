using System.Text;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker.Http;
using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Api.Helpers
{
    public static class SecurityHelper
    {
        public static ClientPrincipal? ParseSwaHeader(HttpRequestData req)
        {
            try
            {
                if (!req.Headers.TryGetValues("x-ms-client-principal", out var headerValues)) return null;
                var header = headerValues.FirstOrDefault();
                if (string.IsNullOrEmpty(header)) return null;

                var data = Convert.FromBase64String(header);
                var json = Encoding.UTF8.GetString(data);
                return JsonSerializer.Deserialize<ClientPrincipal>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                return null;
            }
        }
    }
}
