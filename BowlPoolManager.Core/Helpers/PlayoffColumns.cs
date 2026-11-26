using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Core.Helpers
{
    /// <summary>
    /// The football playoff games of a pool, grouped by round for the printed form. Each round lists its games in
    /// <see cref="GameOrder"/> order (kickoff, id breaks ties), so reading the columns left to right, top to bottom,
    /// follows fast paper entry (owner, 2026-11-25). Blank lines name their feeder game instead of sitting beside it.
    /// </summary>
    public sealed class PlayoffColumns
    {
        public List<BowlGame> FirstRound { get; init; } = new();
        public List<BowlGame> Quarterfinals { get; init; } = new();
        public List<BowlGame> SemiFinals { get; init; } = new();
        public List<BowlGame> Championship { get; init; } = new();

        public static PlayoffColumns Build(IEnumerable<BowlGame> games)
        {
            var playoff = GameOrder.ForEntry(games.Where(g => g.IsPlayoff));
            List<BowlGame> Round(TournamentRound round) => playoff.Where(g => g.Round == round).ToList();
            return new PlayoffColumns
            {
                FirstRound = Round(TournamentRound.Round1),
                Quarterfinals = Round(TournamentRound.QuarterFinal),
                SemiFinals = Round(TournamentRound.SemiFinal),
                Championship = Round(TournamentRound.Championship),
            };
        }
    }
}
