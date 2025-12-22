using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Api.Repositories
{
    public interface IUserRepository
    {
        Task<UserProfile?> GetUserAsync(string id);
        Task UpsertUserAsync(UserProfile user);
        Task<List<UserProfile>> GetUsersAsync();
    }
}
