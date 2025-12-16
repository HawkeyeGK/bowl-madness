using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using BowlPoolManager.Api.Services;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core;
using System.Text.Json;
using BowlPoolManager.Api.Helpers;

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

        [Function("GetMe")]
        public async Task<HttpResponseData> GetMe([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            _logger.LogInformation("Getting current user profile (GetMe).");

            try 
            {
                // 1. Get Identity from SWA Header
                var principal = SecurityHelper.ParseSwaHeader(req);
                if (principal == null || string.IsNullOrEmpty(principal.UserId))
                {
                    // If running locally without emulation, this might be null
                    return req.CreateResponse(HttpStatusCode.Unauthorized);
                }

                // 2. Check if User exists in DB
                var user = await _cosmosService.GetUserAsync(principal.UserId);

                if (user == null)
                {
                    // First time seeing this user: Create Profile
                    user = new UserProfile
                    {
                        Id = principal.UserId,
                        Email = principal.UserDetails ?? string.Empty,
                        DisplayName = principal.UserDetails ?? string.Empty,
                        AppRole = Constants.Roles.Player
                    };

                    // Bootstrap Check: Is this the main admin defined in settings?
                    var bootstrapEmail = _configuration["BootstrapAdminEmail"];
                    if (!string.IsNullOrEmpty(bootstrapEmail) && 
                        user.Email.Equals(bootstrapEmail, StringComparison.OrdinalIgnoreCase))
                    {
                        user.AppRole = Constants.Roles.SuperAdmin;
                    }

                    await _cosmosService.UpsertUserAsync(user);
                    _logger.LogInformation($"Created new user: {user.Email}");
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(user);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetMe failed.");
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }

        [Function("GetUsers")]
        public async Task<HttpResponseData> GetUsers([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            _logger.LogInformation("Getting all users.");

            var principal = SecurityHelper.ParseSwaHeader(req);
            if (principal == null || string.IsNullOrEmpty(principal.UserId))
            {
                return req.CreateResponse(HttpStatusCode.Unauthorized);
            }

            // Authorize (SuperAdmin Only)
            var currentUser = await _cosmosService.GetUserAsync(principal.UserId);
            if (currentUser == null || currentUser.AppRole != Constants.Roles.SuperAdmin)
            {
                _logger.LogWarning($"User {principal.UserId} attempted to access user list without SuperAdmin rights.");
                return req.CreateResponse(HttpStatusCode.Forbidden);
            }

            var users = await _cosmosService.GetUsersAsync();
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(users);
            return response;
        }

        [Function("ToggleUserStatus")]
        public async Task<HttpResponseData> ToggleUserStatus([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            _logger.LogInformation("Toggling user status.");

            try
            {
                // 1. Authenticate & Authorize SuperAdmin
                var principal = SecurityHelper.ParseSwaHeader(req);
                if (principal == null) return req.CreateResponse(HttpStatusCode.Unauthorized);

                var currentUser = await _cosmosService.GetUserAsync(principal.UserId);
                if (currentUser == null || currentUser.AppRole != Constants.Roles.SuperAdmin)
                {
                    return req.CreateResponse(HttpStatusCode.Forbidden);
                }

                // 2. Parse Request
                var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                var targetUserId = query["userId"];
                
                if (string.IsNullOrEmpty(targetUserId))
                {
                    return req.CreateResponse(HttpStatusCode.BadRequest);
                }

                // 3. Update Target User
                var targetUser = await _cosmosService.GetUserAsync(targetUserId);
                if (targetUser == null)
                {
                    return req.CreateResponse(HttpStatusCode.NotFound);
                }

                // Toggle logic
                targetUser.IsDisabled = !targetUser.IsDisabled;

                await _cosmosService.UpsertUserAsync(targetUser);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(targetUser);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ToggleUserStatus failed.");
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }
    }
}
