using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Client.Security
{
    public class StaticWebAppsAuthenticationStateProvider : AuthenticationStateProvider
    {
        private readonly HttpClient _httpClient;

        public StaticWebAppsAuthenticationStateProvider(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            try
            {
                // 1. Get Identity from Azure SWA
                var authData = await _httpClient.GetFromJsonAsync<AuthenticationData>("/.auth/me");
                var principal = authData?.ClientPrincipal;

                if (principal == null)
                {
                    return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
                }

                var identity = new ClaimsIdentity(principal.IdentityProvider);
                identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, principal.UserId));
                identity.AddClaim(new Claim(ClaimTypes.Name, principal.UserDetails));
                identity.AddClaim(new Claim(ClaimTypes.Email, principal.UserDetails));

                // 2. Get Roles from Azure SWA (basic roles like "anonymous", "authenticated")
                foreach (var role in principal.UserRoles)
                {
                    identity.AddClaim(new Claim(ClaimTypes.Role, role));
                }

                // 3. CALL THE API to get Application Roles (SuperAdmin, Player) from Cosmos
                try 
                {
                    // This call performs the sync and retrieves the DB role
                    var response = await _httpClient.PostAsync("/api/user/sync", null);
                    if (response.IsSuccessStatusCode)
                    {
                        var userProfile = await response.Content.ReadFromJsonAsync<UserProfile>();
                        if (userProfile != null && !string.IsNullOrEmpty(userProfile.AppRole))
                        {
                            identity.AddClaim(new Claim(ClaimTypes.Role, userProfile.AppRole));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Auth Error] Failed to sync user profile: {ex.Message}");
                }

                return new AuthenticationState(new ClaimsPrincipal(identity));
            }
            catch
            {
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            }
        }
    }
}
