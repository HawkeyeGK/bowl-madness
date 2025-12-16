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
                // 1. Get Identity from Azure SWA (The "Front Door" Login)
                var authData = await _httpClient.GetFromJsonAsync<AuthenticationData>("/.auth/me");
                var principal = authData?.ClientPrincipal;

                if (principal == null)
                {
                    return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
                }

                // 2. Build Basic Identity
                var identity = new ClaimsIdentity(principal.IdentityProvider);
                identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, principal.UserId));
                identity.AddClaim(new Claim(ClaimTypes.Name, principal.UserDetails));
                identity.AddClaim(new Claim(ClaimTypes.Email, principal.UserDetails));

                foreach (var role in principal.UserRoles)
                {
                    identity.AddClaim(new Claim(ClaimTypes.Role, role));
                }

                // 3. CALL THE API to get Application Roles (SuperAdmin, Player) from Cosmos
                // FIX: Updated to use the new GET endpoint
                try 
                {
                    var userProfile = await _httpClient.GetFromJsonAsync<UserProfile>("api/GetMe");
                    
                    if (userProfile != null && !string.IsNullOrEmpty(userProfile.AppRole))
                    {
                        identity.AddClaim(new Claim(ClaimTypes.Role, userProfile.AppRole));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Auth Error] Failed to sync user profile: {ex.Message}");
                    // Fail Safe: If API is down, we still let them in as a basic user, 
                    // though the MainLayout 'Bouncer' might kick them out anyway if it also fails.
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
