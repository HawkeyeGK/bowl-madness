using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Client.Services;

public interface ISiteContext
{
    Sport ActiveSport { get; }
    bool IsDevMode { get; }
    event Action? OnChange;
    Task InitializeAsync();
    Task SetSportAsync(Sport sport);
}
