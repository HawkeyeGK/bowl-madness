using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Core.Helpers
{
    /// <summary>The line about an entry's pick under a game card, and its tone.</summary>
    public sealed record PickLineResult(string Text, PickOutcome Outcome);

    public static class PickLine
    {
        /// <param name="who">The entry's name ("Sally picked Iowa"); null for "Picked Iowa".</param>
        /// <param name="eliminatedTeams">Teams that have already lost. A pending pick for one of them can no longer score, so it is shown as wrong.</param>
        public static PickLineResult For(BowlGame game, string? pick, string? who, ISet<string>? eliminatedTeams = null)
        {
            var outcome = PickOutcomes.For(game, pick);
            var picked = who == null ? "Picked" : $"{who} picked";

            return outcome switch
            {
                PickOutcome.NoPick => new PickLineResult(who == null ? "No pick for this game" : $"{who} made no pick for this game", outcome),
                PickOutcome.Correct => new PickLineResult($"{picked} {pick} ✓ +{game.PointValue}", outcome),
                PickOutcome.Wrong => new PickLineResult($"{picked} {pick} ✗", outcome),
                _ when eliminatedTeams != null && eliminatedTeams.Contains(pick!) =>
                    new PickLineResult($"{picked} {pick}, who is already out", PickOutcome.Wrong),
                _ => new PickLineResult($"{picked} {pick}", outcome)
            };
        }
    }
}
