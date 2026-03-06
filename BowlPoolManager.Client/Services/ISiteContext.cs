namespace BowlPoolManager.Client.Services;

public enum Sport
{
    Football,
    Basketball
}

public interface ISiteContext
{
    Sport ActiveSport { get; }
    bool IsDevMode { get; }
    event Action? OnChange;
    Task InitializeAsync();
    Task SetSportAsync(Sport sport);
}
