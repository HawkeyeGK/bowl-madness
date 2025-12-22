using Microsoft.Azure.Cosmos;
using BowlPoolManager.Api.Infrastructure;
using BowlPoolManager.Core;
using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Api.Repositories
{
    public class UserRepository : CosmosRepositoryBase, IUserRepository
    {
        public UserRepository(Container container) : base(container) { }

        public async Task<UserProfile?> GetUserAsync(string id) => await GetDocumentAsync<UserProfile>(id);
        public async Task UpsertUserAsync(UserProfile user) => await UpsertDocumentAsync(user, user.Id);
        public async Task<List<UserProfile>> GetUsersAsync() => await GetListAsync<UserProfile>(Constants.DocumentTypes.UserProfile);
    }
}
