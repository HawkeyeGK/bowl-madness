using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Api.Repositories
{
    public interface IEntryRepository
    {
        Task AddEntryAsync(BracketEntry entry);
        Task<List<BracketEntry>> GetEntriesAsync(string? poolId = null);
        Task<BracketEntry?> GetEntryAsync(string id);
        Task DeleteEntryAsync(string id);
        Task<List<BracketEntry>> GetEntriesForUserAsync(string userId, string poolId);
        Task<bool> IsBracketNameTakenAsync(string poolId, string bracketName, string? excludeId = null);
    }
}
