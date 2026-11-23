using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.ScoreFeed;

namespace BowlPoolManager.Api.Services.ScoreFeed
{
    /// <summary>
    /// Undoes what the mock feed did to a season's football games: scores, status, detail, and the teams that
    /// propagation moved into later games. Propagation leaves no marker, so a slot is restored only when it holds
    /// the feeder's winner (by name). Any other slot is left alone and reported.
    /// </summary>
    public static class MockFeedReset
    {
        public record SlotClear(string NextGameId, bool Home, string FeederBowlName);

        public class Plan
        {
            public List<string> GameIds { get; } = new();
            public List<string> GameNames { get; } = new();
            public List<SlotClear> SlotClears { get; } = new();
            public List<string> NeedsReview { get; } = new();
            public List<string> LeftAlone { get; } = new();

            public int GameCount => GameIds.Count;

            public MockFeedResetSummary ToSummary() => new()
            {
                GamesToReset = GameCount,
                GameNames = GameNames.ToList(),
                SlotsToClear = SlotClears.Count,
                NeedsReview = NeedsReview.ToList(),
                LeftAlone = LeftAlone.ToList()
            };
        }

        // Plans only; changes nothing.
        public static Plan Build(IReadOnlyList<BowlGame> games)
        {
            var plan = new Plan();
            var byId = new Dictionary<string, BowlGame>(StringComparer.OrdinalIgnoreCase);
            foreach (var g in games) byId.TryAdd(g.Id, g);

            var resetGames = games.Where(g => IsLinked(g) && HasResult(g)).ToList();
            var resetIds = new HashSet<string>(resetGames.Select(g => g.Id), StringComparer.OrdinalIgnoreCase);

            foreach (var g in resetGames)
            {
                plan.GameIds.Add(g.Id);
                plan.GameNames.Add(g.BowlName);

                var winner = g.WinningTeamName;
                if (string.IsNullOrEmpty(winner) || string.IsNullOrEmpty(g.NextGameId)) continue;
                if (!byId.TryGetValue(g.NextGameId, out var next)) continue;

                var homeHolds = Same(next.TeamHome, winner);
                var awayHolds = Same(next.TeamAway, winner);
                if (homeHolds == awayHolds)
                {
                    plan.LeftAlone.Add(homeHolds
                        ? $"{next.BowlName}: both slots hold {winner} (from {g.BowlName}); left as is."
                        : $"{next.BowlName}: neither slot holds {winner} (from {g.BowlName}); left as is.");
                    continue;
                }

                if (!resetIds.Contains(next.Id) && HasResult(next))
                {
                    plan.NeedsReview.Add($"{next.BowlName}: has a result the mock did not produce; {winner} (from {g.BowlName}) left in place.");
                    continue;
                }

                plan.SlotClears.Add(new SlotClear(next.Id, homeHolds, g.BowlName));
            }

            return plan;
        }

        // Applies the plan to the given games in memory. Returns the games that changed, for saving.
        public static List<BowlGame> Apply(Plan plan, IReadOnlyList<BowlGame> games)
        {
            var byId = new Dictionary<string, BowlGame>(StringComparer.OrdinalIgnoreCase);
            foreach (var g in games) byId.TryAdd(g.Id, g);

            var changed = new List<BowlGame>();
            void Mark(BowlGame g) { if (!changed.Contains(g)) changed.Add(g); }

            foreach (var id in plan.GameIds)
            {
                var g = byId[id];
                g.TeamHomeScore = null;
                g.TeamAwayScore = null;
                g.Status = GameStatus.Scheduled;
                g.GameDetail = null;
                Mark(g);
            }

            foreach (var clear in plan.SlotClears)
            {
                var next = byId[clear.NextGameId];
                var placeholder = $"Winner of {clear.FeederBowlName}";
                if (clear.Home)
                {
                    next.TeamHome = placeholder;
                    next.HomeTeamInfo = null;
                    next.TeamHomeSeed = null;
                }
                else
                {
                    next.TeamAway = placeholder;
                    next.AwayTeamInfo = null;
                    next.TeamAwaySeed = null;
                }
                Mark(next);
            }

            return changed;
        }

        private static bool IsLinked(BowlGame g) => !string.IsNullOrEmpty(g.ExternalId);

        private static bool HasResult(BowlGame g) =>
            g.Status != GameStatus.Scheduled || g.TeamHomeScore != null || g.TeamAwayScore != null || !string.IsNullOrEmpty(g.GameDetail);

        private static bool Same(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
