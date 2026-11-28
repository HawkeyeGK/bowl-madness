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

                // A disabled account gets no profile and no role. The 403 (not 401) is how the browser learns the
                // account is disabled, so it can say so before signing the user out (issue 98).
                if (user != null && user.IsDisabled)
                {
                    return req.CreateResponse(HttpStatusCode.Forbidden);
                }

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
                    _logger.LogInformation("Created new user {UserId}.", user.Id);
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

            var caller = await SecurityHelper.GetCallerAsync(req, _userRepo);
            if (caller == null)
            {
                return req.CreateResponse(HttpStatusCode.Unauthorized);
            }

            // Allow Admin access for Entry Management (needed to see user emails)
            if (!caller.IsAdmin)
            {
                _logger.LogWarning($"User {caller.UserId} attempted to access user list without Admin rights.");
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
                var caller = await SecurityHelper.GetCallerAsync(req, _userRepo);
                if (caller == null) return req.CreateResponse(HttpStatusCode.Unauthorized);

                if (!caller.IsSuperAdmin)
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

                if (SuperAdminGuard.RemovesAnActiveSuperAdmin(targetUser, targetUser.AppRole, !targetUser.IsDisabled)
                    && SuperAdminGuard.LeavesNoActiveSuperAdmin(await _userRepo.GetUsersAsync(), targetUser, targetUser.AppRole, !targetUser.IsDisabled))
                {
                    var refused = req.CreateResponse(HttpStatusCode.BadRequest);
                    await refused.WriteStringAsync(SuperAdminGuard.Refusal);
                    return refused;
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
        [Function("UpdateUserRole")]
        public async Task<HttpResponseData> UpdateUserRole([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            _logger.LogInformation("Updating user role.");

            try
            {
                var caller = await SecurityHelper.GetCallerAsync(req, _userRepo);
                if (caller == null) return req.CreateResponse(HttpStatusCode.Unauthorized);

                if (!caller.IsSuperAdmin)
                {
                    return req.CreateResponse(HttpStatusCode.Forbidden);
                }

                var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                var targetUserId = query["userId"];
                var newRole = query["role"];

                if (string.IsNullOrEmpty(targetUserId) || string.IsNullOrEmpty(newRole))
                {
                    return req.CreateResponse(HttpStatusCode.BadRequest);
                }

                if (newRole != Constants.Roles.Admin && newRole != Constants.Roles.Player && newRole != Constants.Roles.SuperAdmin)
                {
                    return req.CreateResponse(HttpStatusCode.BadRequest);
                }

                // Use Repo
                var targetUser = await _userRepo.GetUserAsync(targetUserId);
                if (targetUser == null)
                {
                    return req.CreateResponse(HttpStatusCode.NotFound);
                }

                if (SuperAdminGuard.RemovesAnActiveSuperAdmin(targetUser, newRole, targetUser.IsDisabled)
                    && SuperAdminGuard.LeavesNoActiveSuperAdmin(await _userRepo.GetUsersAsync(), targetUser, newRole, targetUser.IsDisabled))
                {
                    var refused = req.CreateResponse(HttpStatusCode.BadRequest);
                    await refused.WriteStringAsync(SuperAdminGuard.Refusal);
                    return refused;
                }

                targetUser.AppRole = newRole;

                // Use Repo
                await _userRepo.UpsertUserAsync(targetUser);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(targetUser);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdateUserRole failed.");
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }
    }
}
