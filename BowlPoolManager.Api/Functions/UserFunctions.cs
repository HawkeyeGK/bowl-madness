using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using BowlPoolManager.Api.Services;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core; // Added reference
using System.Text.Json;
using System.Text;
using BowlPoolManager.Api.Helpers; // Added reference

namespace BowlPoolManager.Api.Functions
{
    public class UserFunctions
    {
        private readonly ILogger _logger;
        private readonly ICosmosDbService _cosmosService;
        private readonly IConfiguration _configuration;

        public UserFunctions(ILoggerFactory loggerFactory, ICosmosDbService cosmosService, IConfiguration configuration)
        {
            _logger = loggerFactory.CreateLogger<UserFunctions>();
            _cosmosService = cosmosService;
            _configuration = configuration;
        }

        [Function("SyncUser")]
        public async Task<HttpResponseData> SyncUser([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "user/sync")] HttpRequestData req)
        {
            _logger.LogInformation("Syncing user profile.");

            try 
            {
                // 1. Get Identity from SWA Header
                // 1. Get Identity from SWA Header
                var principal = SecurityHelper.ParseSwaHeader(req);
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

                    var bootstrapEmail = _configuration["BootstrapAdminEmail"];

                    if (!string.IsNullOrEmpty(bootstrapEmail) && user.Email.Equals(bootstrapEmail, StringComparison.OrdinalIgnoreCase))
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

    }
}
