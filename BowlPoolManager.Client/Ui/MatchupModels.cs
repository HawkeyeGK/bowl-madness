namespace BowlPoolManager.Client.Ui;

/// <summary>Where a game stands, for <see cref="MatchupCard"/>.</summary>
public enum MatchupStatus
{
    /// <summary>Not started: the status text is the kickoff time.</summary>
    Scheduled,
    /// <summary>Being played: red, pulsing; the status text is the clock ("Q3 · 4:12").</summary>
    Live,
    /// <summary>Over: the winner is bold and the loser is dimmed.</summary>
    Final
}

/// <summary>One side of a <see cref="MatchupCard"/>.</summary>
/// <param name="Name">The team, "Winner of Rose Bowl", or "TBD".</param>
/// <param name="Info">Logo and colors; null draws the empty logo tile.</param>
/// <param name="ApRank">AP rank, drawn "#15" before the name.</param>
/// <param name="Seed">Playoff seed, drawn "(7)" after the name.</param>
/// <param name="Score">Current or final score; null before kickoff.</param>
/// <param name="Decided">False for a slot that is not decided yet (name in italics).</param>
/// <param name="Won">The game is final and this side won (bold).</param>
public sealed record MatchupTeam(
    string Name,
    BowlPoolManager.Core.Domain.TeamInfo? Info,
    int? ApRank = null,
    int? Seed = null,
    int? Score = null,
    bool Decided = true,
    bool Won = false);

/// <summary>A line under the teams about one entry's pick, for example "Sally picked Iowa". The tone follows the outcome.</summary>
public sealed record MatchupPick(string Text, BowlPoolManager.Core.Helpers.PickOutcome Outcome);
