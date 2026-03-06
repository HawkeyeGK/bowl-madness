namespace BowlPoolManager.Core.Domain
{
    public interface IScorable
    {
        string Id { get; }
        GameStatus Status { get; }
        int PointValue { get; }
        TournamentRound Round { get; }
        string TeamHome { get; }
        string TeamAway { get; }
        int? TeamHomeScore { get; }
        int? TeamAwayScore { get; }
        bool IsFinal { get; }
        string? WinningTeamName { get; }
    }
}
