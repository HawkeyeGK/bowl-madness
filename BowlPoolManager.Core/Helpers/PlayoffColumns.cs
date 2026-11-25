using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Core.Helpers
{
    /// <summary>
    /// The football playoff games of a pool, grouped by round and ordered for drawing a tree: each round lists its
    /// games in the order of the games they feed, so lines between columns do not cross.
    /// </summary>
    public sealed class PlayoffColumns
    {
        public List<BowlGame> FirstRound { get; init; } = new();
        public List<BowlGame> Quarterfinals { get; init; } = new();
        public List<BowlGame> SemiFinals { get; init; } = new();
        public List<BowlGame> Championship { get; init; } = new();

        public static PlayoffColumns Build(IEnumerable<BowlGame> games)
        {
            var playoff = games.Where(g => g.IsPlayoff).ToList();
            var champ = playoff.Where(g => g.Round == TournamentRound.Championship).OrderBy(g => g.StartTime).ToList();
            var semi = Sort(playoff, TournamentRound.SemiFinal, champ);
            var quarter = Sort(playoff, TournamentRound.QuarterFinal, semi);
            var first = Sort(playoff, TournamentRound.Round1, quarter);
            return new PlayoffColumns { FirstRound = first, Quarterfinals = quarter, SemiFinals = semi, Championship = champ };
        }

        private static List<BowlGame> Sort(List<BowlGame> playoff, TournamentRound round, List<BowlGame> nextRoundSorted) =>
            playoff.Where(g => g.Round == round)
                   .OrderBy(g =>
                   {
                       var index = nextRoundSorted.FindIndex(n => n.Id == g.NextGameId);
                       return index == -1 ? int.MaxValue : index;
                   })
                   .ThenBy(g => g.StartTime)
                   .ToList();
    }
}
