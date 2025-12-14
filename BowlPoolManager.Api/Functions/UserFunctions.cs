using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using BowlPoolManager.Api.Services;
using BowlPoolManager.Core.Domain;
using System.Text.Json;
using System.Text;

namespace BowlPoolManager.Api.Functions
{
    public class UserFunctions
    {
        private readonly ILogger _logger;
        private readonly ICosmosDbService _cosmosService;
        
        // Bootstrapping the SuperAdmin using the Director's email
        private const string BOOTSTRAP_ADMIN_EMAIL = "JasonRNash@gmail.com"; 

        public UserFunctions(ILoggerFactory loggerFactory, ICosmosDbService cosmosService)
        {
            _logger = loggerFactory.CreateLogger<UserFunctions>();
            _cosmosService = cosmosService;
        }

        [Function("SyncUser")]
        public async Task<HttpResponseData> SyncUser([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "user/sync")] HttpRequestData req)
        {
            _logger.LogInformation("Syncing user profile.");

            // 1. Get Identity from SWA Header
            var principal = ParseSwaHeader(req);
            if (principal == null || string.IsNullOrEmpty(principal.UserId))
            {
                return req.CreateResponse(HttpStatusCode.Unauthorized);
            }

            // 2. Check if User exists in DB
            var user = await _cosmosService.GetUserAsync(principal.UserId);

            if (user == null)
            {
                // 3. Create New User
                user = new UserProfile
                {
                    Id = principal.UserId,
                    Email = principal.UserDetails ?? string.Empty, // SWA maps email to UserDetails for Google
                    DisplayName = principal.UserDetails ?? string.Empty,
                    AppRole = "Player"
                };

                // 4. Bootstrap Super Admin logic
                if (user.Email.Equals(BOOTSTRAP_ADMIN_EMAIL, StringComparison.OrdinalIgnoreCase))
                {
                    user.AppRole = "SuperAdmin";
                    _logger.LogWarning($"Bootstrapping SuperAdmin: {user.Email}");
                }

                await _cosmosService.UpsertUserAsync(user);
            }

            // 5. Return the profile
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(user);
            return response;
        }

        private ClientPrincipal? ParseSwaHeader(HttpRequestData req)
        {
            if (!req.Headers.TryGetValues("x-ms-client-principal", out var headerValues)) return null;
            var header = headerValues.FirstOrDefault();
            if (string.IsNullOrEmpty(header)) return null;

            var data = Convert.FromBase64String(header);
            var json = Encoding.UTF8.GetString(data);
            return JsonSerializer.Deserialize<ClientPrincipal>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
    }
}
