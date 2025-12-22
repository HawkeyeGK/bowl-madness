using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core;
using BowlPoolManager.Api.Helpers;
using BowlPoolManager.Api.Repositories; // NEW

namespace BowlPoolManager.Api.Functions
{
    public class UserFunctions
    {
        private readonly ILogger _logger;
        private readonly IUserRepository _userRepo; // Changed
        private readonly IConfiguration _configuration;

        public UserFunctions(ILoggerFactory loggerFactory, IUserRepository userRepo, IConfiguration configuration)
        {
            _logger = loggerFactory.CreateLogger<UserFunctions>();
            _userRepo = userRepo;
            _configuration = configuration;
        }

        [Function("GetMe")]
        public async Task<HttpResponseData> GetMe([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            _logger.LogInformation("Getting current user profile (GetMe).");

            try 
            {
                var principal = SecurityHelper.ParseSwaHeader(req);
                if (principal == null || string.IsNullOrEmpty(principal.UserId))
                {
                    return req.CreateResponse(HttpStatusCode.Unauthorized);
                }

                // Use Repo
                var user = await _userRepo.GetUserAsync(principal.UserId);

                if (user == null)
                {
                    user = new UserProfile
                    {
                        Id = principal.UserId,
                        Email = principal.UserDetails ?? string.Empty,
                        DisplayName = principal.UserDetails ?? string.Empty,
                        AppRole = Constants.Roles.Player
                    };

                    var bootstrapEmail = _configuration["BootstrapAdminEmail"];
                    if (!string.IsNullOrEmpty(bootstrapEmail) && 
                        user.Email.Equals(bootstrapEmail, StringComparison.OrdinalIgnoreCase))
                    {
                        user.AppRole = Constants.Roles.SuperAdmin;
                    }

                    // Use Repo
                    await _userRepo.UpsertUserAsync(user);
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

            // Use Repo for Authorization check
            var currentUser = await _userRepo.GetUserAsync(principal.UserId);
            if (currentUser == null || currentUser.AppRole != Constants.Roles.SuperAdmin)
            {
                _logger.LogWarning($"User {principal.UserId} attempted to access user list without SuperAdmin rights.");
                return req.CreateResponse(HttpStatusCode.Forbidden);
            }

            // Use Repo
            var users = await _userRepo.GetUsersAsync();
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
                var principal = SecurityHelper.ParseSwaHeader(req);
                if (principal == null) return req.CreateResponse(HttpStatusCode.Unauthorized);

                // Use Repo
                var currentUser = await _userRepo.GetUserAsync(principal.UserId);
                if (currentUser == null || currentUser.AppRole != Constants.Roles.SuperAdmin)
                {
                    return req.CreateResponse(HttpStatusCode.Forbidden);
                }

                var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                var targetUserId = query["userId"];
                
                if (string.IsNullOrEmpty(targetUserId))
                {
                    return req.CreateResponse(HttpStatusCode.BadRequest);
                }

                // Use Repo
                var targetUser = await _userRepo.GetUserAsync(targetUserId);
                if (targetUser == null)
                {
                    return req.CreateResponse(HttpStatusCode.NotFound);
                }

                targetUser.IsDisabled = !targetUser.IsDisabled;

                // Use Repo
                await _userRepo.UpsertUserAsync(targetUser);

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
