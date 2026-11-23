using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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

        /// <summary>
        /// A signed-in caller whose account is allowed to use the site. Profile is null for a user who has
        /// signed in but not yet been given a profile (the first GetMe call creates it).
        /// </summary>
        public sealed class Caller
        {
            public Caller(ClientPrincipal principal, UserProfile? profile)
            {
                Principal = principal;
                Profile = profile;
            }

            public ClientPrincipal Principal { get; }
            public UserProfile? Profile { get; }
            public string UserId => Principal.UserId;
            public bool IsAdmin => SecurityHelper.IsAdmin(Profile);
            public bool IsSuperAdmin => Profile != null && Profile.AppRole == Constants.Roles.SuperAdmin;
        }

        /// <summary>
        /// Reads the sign-in header. A missing header, a header that is not valid base64, a body that is not a
        /// principal object, or a principal with no user id all count as not signed in (null), never an exception.
        /// Does not look at the user profile: use GetCallerAsync for anything that is not GetMe.
        /// </summary>
        public static ClientPrincipal? ParseSwaHeader(HttpRequestData req)
        {
            if (!req.Headers.TryGetValues("x-ms-client-principal", out var headerValues))
            {
                return null;
            }

            return ParsePrincipal(headerValues.FirstOrDefault());
        }

        public static ClientPrincipal? ParseSwaHeader(HttpRequest req)
        {
            return ParsePrincipal(req.Headers.TryGetValue("x-ms-client-principal", out var headerValues)
                ? headerValues.FirstOrDefault()
                : null);
        }

        private static ClientPrincipal? ParsePrincipal(string? header)
        {
            if (string.IsNullOrEmpty(header)) return null;

            try
            {
                var data = Convert.FromBase64String(header);
                var decoded = Encoding.UTF8.GetString(data);

                var principal = JsonSerializer.Deserialize<ClientPrincipal>(decoded, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return principal == null || string.IsNullOrEmpty(principal.UserId) ? null : principal;
            }
            catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
            {
                return null;
            }
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

        /// <summary>
        /// The one place that decides who is calling. Returns null (answer 401) when there is no valid sign-in
        /// header or the account is disabled: a disabled user, including a disabled admin, is treated exactly
        /// like a signed-out one (BUSINESS-RULES section 1).
        /// </summary>
        public static async Task<Caller?> GetCallerAsync(HttpRequestData req, IUserRepository userRepo)
            => await ToCallerAsync(ParseSwaHeader(req), userRepo);

        public static async Task<Caller?> GetCallerAsync(HttpRequest req, IUserRepository userRepo)
            => await ToCallerAsync(ParseSwaHeader(req), userRepo);

        private static async Task<Caller?> ToCallerAsync(ClientPrincipal? principal, IUserRepository userRepo)
        {
            if (principal == null) return null;

            var profile = await userRepo.GetUserAsync(principal.UserId);
            if (profile != null && profile.IsDisabled) return null;

            return new Caller(principal, profile);
        }

        public static async Task<AuthResult> ValidateSuperAdminAsync(HttpRequestData req, IUserRepository userRepo)
        {
            var caller = await GetCallerAsync(req, userRepo);
            if (caller == null)
            {
                return new AuthResult
                {
                    IsValid = false,
                    ErrorResponse = req.CreateResponse(System.Net.HttpStatusCode.Unauthorized)
                };
            }

            if (!caller.IsSuperAdmin)
            {
                return new AuthResult
                {
                    IsValid = false,
                    ErrorResponse = req.CreateResponse(System.Net.HttpStatusCode.Forbidden)
                };
            }

            return new AuthResult { IsValid = true };
        }

        /// <summary>
        /// Same check for the functions that use the ASP.NET request type. Returns the refusal (401 or 403),
        /// or null when the caller is a SuperAdmin.
        /// </summary>
        public static async Task<IActionResult?> RefuseUnlessSuperAdminAsync(HttpRequest req, IUserRepository userRepo)
        {
            var caller = await GetCallerAsync(req, userRepo);
            if (caller == null) return new StatusCodeResult(StatusCodes.Status401Unauthorized);
            if (!caller.IsSuperAdmin) return new StatusCodeResult(StatusCodes.Status403Forbidden);
            return null;
        }
    }
}
