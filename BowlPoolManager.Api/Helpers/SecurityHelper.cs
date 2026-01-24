using Microsoft.Azure.Functions.Worker.Http;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core;
using BowlPoolManager.Api.Repositories; // NEW
using System.Text.Json;
using System.Text;

namespace BowlPoolManager.Api.Helpers
{
    public static class SecurityHelper
    {
        public class ClientPrincipal
        {
            public string IdentityProvider { get; set; } = string.Empty;
            public string UserId { get; set; } = string.Empty;
            public string UserDetails { get; set; } = string.Empty;
            public IEnumerable<string> UserRoles { get; set; } = new List<string>();
        }

        public static ClientPrincipal? ParseSwaHeader(HttpRequestData req)
        {
            if (!req.Headers.TryGetValues("x-ms-client-principal", out var headerValues))
            {
                return null;
            }

            var header = headerValues.FirstOrDefault();
            if (string.IsNullOrEmpty(header)) return null;

            var data = Convert.FromBase64String(header);
            var decoded = Encoding.UTF8.GetString(data);
            
            return JsonSerializer.Deserialize<ClientPrincipal>(decoded, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        public class AuthResult
        {
            public bool IsValid { get; set; }
            public HttpResponseData? ErrorResponse { get; set; }
        }

        public static bool IsAdmin(UserProfile? user)
        {
            return user != null && (user.AppRole == Constants.Roles.SuperAdmin || user.AppRole == Constants.Roles.Admin);
        }

        public static async Task<AuthResult> ValidateSuperAdminAsync(HttpRequestData req, IUserRepository userRepo)
        {
            var principal = ParseSwaHeader(req);
            if (principal == null || string.IsNullOrEmpty(principal.UserId))
            {
                return new AuthResult 
                { 
                    IsValid = false, 
                    ErrorResponse = req.CreateResponse(System.Net.HttpStatusCode.Unauthorized) 
                };
            }

            // DB LOOKUP using IUserRepository
            var userProfile = await userRepo.GetUserAsync(principal.UserId);
            if (userProfile == null || userProfile.AppRole != Constants.Roles.SuperAdmin)
            {
                return new AuthResult 
                { 
                    IsValid = false, 
                    ErrorResponse = req.CreateResponse(System.Net.HttpStatusCode.Forbidden) 
                };
            }

            return new AuthResult { IsValid = true };
        }
    }
}
