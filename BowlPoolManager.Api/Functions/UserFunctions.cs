using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using BowlPoolManager.Api.Services;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core; // Added reference
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

            try 
            {
                // 1. Get Identity from SWA Header
                var principal = ParseSwaHeader(req);
                if (principal == null || string.IsNullOrEmpty(principal.UserId))
                {
                    _logger.LogWarning("No SWA Principal found.");
                    return req.CreateResponse(HttpStatusCode.Unauthorized);
                }

                // 2. Check if User exists in DB
                // This line will throw if DB connection is bad
                var user = await _cosmosService.GetUserAsync(principal.UserId);

                if (user == null)
                {
                    user = new UserProfile
                    {
                        Id = principal.UserId,
                        Email = principal.UserDetails ?? string.Empty,
                        DisplayName = principal.UserDetails ?? string.Empty,
                        // UPDATED: Using Constants
                        AppRole = Constants.Roles.Player
                    };

                    if (user.Email.Equals(BOOTSTRAP_ADMIN_EMAIL, StringComparison.OrdinalIgnoreCase))
                    {
                        // UPDATED: Using Constants
                        user.AppRole = Constants.Roles.SuperAdmin;
                    }

                    await _cosmosService.UpsertUserAsync(user);
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(user);
                return response;
            }
            catch (Exception ex)
            {
                // DEBUGGING: Return the crash details to the client
                _logger.LogError(ex, "SyncUser Crashed");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"CRASH REPORT: {ex.Message} | Trace: {ex.StackTrace}");
                return errorResponse;
            }
        }

        private ClientPrincipal? ParseSwaHeader(HttpRequestData req)
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
