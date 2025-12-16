using System.Text;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker.Http;
using BowlPoolManager.Core.Domain;
using System.Net;
using BowlPoolManager.Api.Services;
using BowlPoolManager.Core;

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

        /// <summary>
        /// Validates that the request comes from an authenticated user who is a SuperAdmin.
        /// Returns (true, null) if valid.
        /// Returns (false, HttpResponseData) if invalid (Unauthorized or Forbidden).
        /// </summary>
        public static async Task<(bool IsValid, HttpResponseData? ErrorResponse)> ValidateSuperAdminAsync(
            HttpRequestData req, 
            ICosmosDbService cosmosService)
        {
            // 1. Authenticate
            var principal = ParseSwaHeader(req);
            if (principal == null || string.IsNullOrEmpty(principal.UserId))
            {
                return (false, req.CreateResponse(HttpStatusCode.Unauthorized));
            }

            // 2. Authorize (SuperAdmin Only)
            var userProfile = await cosmosService.GetUserAsync(principal.UserId);
            if (userProfile == null || userProfile.AppRole != Constants.Roles.SuperAdmin)
            {
                // Log warning here if you had a logger passed in, otherwise relies on caller logging
                return (false, req.CreateResponse(HttpStatusCode.Forbidden));
            }

            return (true, null);
        }
    }
}
