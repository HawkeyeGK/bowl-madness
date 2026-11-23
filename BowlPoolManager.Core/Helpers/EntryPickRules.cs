using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Core.Helpers
{
    /// <summary>
    /// The two teams an entry may pick between in one game. A null team means the slot is not known yet: the game
    /// that feeds it has no valid pick from this entry. <see cref="HomeLabel"/> and <see cref="AwayLabel"/> are what
    /// to show ("Winner of {bowl}" or "TBD" when the slot is not known). <see cref="HomeWaitsFor"/> names the feeding
    /// game when that is why the slot is not known.
    /// </summary>
    public sealed record Matchup(string? Home, string? Away, string HomeLabel, string AwayLabel, string? HomeWaitsFor, string? AwayWaitsFor);

    public sealed record ClearResult(Dictionary<string, string> Picks, List<string> ClearedGameIds);

    /// <summary>
    /// Which team an entry may pick in each football game, given the entry's own earlier picks (issue 128).
    /// A slot with a real team (a seed, or a winner that has already been copied in) holds that team. A slot
    /// that still waits for a feeder game takes the entry's pick in that game, but only when that pick is itself
    /// one of that game's two teams. Which slot a feeder fills comes from <see cref="BracketSlots"/>.
    /// </summary>
    public static class EntryPickRules
    {
        /// <param name="games">Every game of the season (feeders are looked up here).</param>
        public static Dictionary<string, Matchup> Resolve(IReadOnlyList<BowlGame> games, IReadOnlyDictionary<string, string>? picks)
        {
            var result = new Dictionary<string, Matchup>(StringComparer.OrdinalIgnoreCase);
            var byId = games.GroupBy(g => g.Id, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            var resolving = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var game in games) ResolveGame(game);
            return result;

            Matchup ResolveGame(BowlGame game)
            {
                if (result.TryGetValue(game.Id, out var done)) return done;

                // A feeder loop is a data error: a game met again while it is being worked out has no known slots.
                if (!resolving.Add(game.Id)) return new Matchup(null, null, "TBD", "TBD", null, null);

                var feeders = BracketSlots.FeedersOf(game, games);
                var homeFeeders = feeders.Where(f => BracketSlots.IsHomeSlot(f, game, games)).ToList();
                var awayFeeders = feeders.Where(f => !BracketSlots.IsHomeSlot(f, game, games)).ToList();

                var home = Slot(game.TeamHome, homeFeeders);
                var away = Slot(game.TeamAway, awayFeeders);
                var matchup = new Matchup(home.Team, away.Team, home.Label, away.Label, home.WaitsFor, away.WaitsFor);

                resolving.Remove(game.Id);
                result[game.Id] = matchup;
                return matchup;
            }

            (string? Team, string Label, string? WaitsFor) Slot(string stored, List<BowlGame> feeders)
            {
                if (!BracketSlots.IsPlaceholder(stored)) return (stored, stored, null);

                // Exactly one feeder fills a slot; more than that is a data error and the slot stays unknown.
                if (feeders.Count != 1)
                    return (null, string.IsNullOrWhiteSpace(stored) ? "TBD" : stored, null);

                var feeder = feeders[0];
                var label = BracketSlots.PlaceholderFor(feeder);
                var feederMatchup = ResolveGame(feeder);

                if (picks != null && picks.TryGetValue(feeder.Id, out var pick) && TeamOf(feederMatchup, pick) is { } team)
                    return (team, team, null);

                return (null, label, feeder.BowlName);
            }
        }

        /// <summary>True when <paramref name="pick"/> is one of the two known teams of the matchup.</summary>
        public static bool IsValidPick(Matchup matchup, string? pick) => TeamOf(matchup, pick) != null;

        /// <summary>The matchup's own spelling of the picked team, or null when the pick is not one of its known teams.</summary>
        private static string? TeamOf(Matchup matchup, string? pick)
        {
            if (string.IsNullOrWhiteSpace(pick)) return null;
            if (matchup.Home != null && string.Equals(matchup.Home, pick, StringComparison.OrdinalIgnoreCase)) return matchup.Home;
            if (matchup.Away != null && string.Equals(matchup.Away, pick, StringComparison.OrdinalIgnoreCase)) return matchup.Away;
            return null;
        }

        /// <summary>
        /// Returns a copy of the picks without those that are not a reachable team (junk text, or a team the
        /// entry's own earlier picks no longer lead to). <see cref="ClearResult.ClearedGameIds"/> lists the games
        /// that lost a real pick, in kickoff order; an empty pick is dropped without being listed.
        /// </summary>
        public static ClearResult ClearInvalid(IReadOnlyList<BowlGame> games, IReadOnlyDictionary<string, string>? picks)
        {
            var kept = new Dictionary<string, string>(picks ?? new Dictionary<string, string>(), StringComparer.Ordinal);
            var cleared = new List<string>();

            // Removing a pick can make a later pick invalid, so repeat until nothing changes.
            bool changed = true;
            while (changed)
            {
                changed = false;
                var matchups = Resolve(games, kept);
                foreach (var key in kept.Keys.ToList())
                {
                    // A pick for a game that is not in the list is not this method's business (the server decides that).
                    if (!matchups.TryGetValue(key, out var m) || IsValidPick(m, kept[key])) continue;

                    if (!string.IsNullOrWhiteSpace(kept[key])) cleared.Add(key);
                    kept.Remove(key);
                    changed = true;
                }
            }

            var order = games.OrderBy(g => g.StartTime).ThenBy(g => g.Id, StringComparer.Ordinal).Select(g => g.Id).ToList();
            cleared = cleared.Distinct().OrderBy(id => { var i = order.IndexOf(id); return i < 0 ? int.MaxValue : i; }).ToList();
            return new ClearResult(kept, cleared);
        }

        /// <summary>How many of the pool's games have a valid pick.</summary>
        public static int CountValid(IReadOnlyList<BowlGame> games, IEnumerable<string> poolGameIds, IReadOnlyDictionary<string, string>? picks)
        {
            if (picks == null) return 0;
            var matchups = Resolve(games, picks);
            return poolGameIds.Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(id => picks.TryGetValue(id, out var pick) && matchups.TryGetValue(id, out var m) && IsValidPick(m, pick));
        }
    }
}
