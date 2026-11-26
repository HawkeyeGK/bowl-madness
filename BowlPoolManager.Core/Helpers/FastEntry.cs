using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Core.Helpers
{
    /// <param name="Sequence">The new digits with later games that depended on a changed game set back to 0.</param>
    /// <param name="ClearedGameIds">Games that lost a digit or a saved pick, in entry order.</param>
    public sealed record RekeyResult(string Sequence, IReadOnlyList<string> ClearedGameIds);

    /// <param name="Picks">The entry's picks after the digits: stored picks the digits leave alone, plus the keyed ones.</param>
    /// <param name="Sequence">The digits as kept: only 0, 1 and 2, at most one per game.</param>
    /// <param name="NotKnownGameIds">Games keyed 1 or 2 whose team is not known, because a game it depends on is blank.</param>
    public sealed record SequenceResult(Dictionary<string, string> Picks, string Sequence, IReadOnlyList<string> NotKnownGameIds);

    /// <summary>
    /// Fast paper entry (prototype 4, owner decision 2026-11-25): the keyer types one digit per game, reading down the
    /// printed form, in the order of <see cref="GameOrder"/>. 1 picks the team on the form's first line, 2 the team on
    /// its second line, 0 leaves the game blank ("-" and space count as 0). A regular bowl lists the away team first; a
    /// playoff game lists its home team first (<see cref="BlankFormSlots.HomeOnTop"/>). A playoff team is only
    /// picked when the entry's own picks lead to it (<see cref="EntryPickRules"/>), the same rule the server applies.
    /// </summary>
    public static class FastEntry
    {
        /// <param name="ordered">The pool's games in entry order.</param>
        /// <param name="allGames">Every game of the season (feeders are looked up here).</param>
        /// <param name="stored">
        /// The entry's saved picks. A game keyed 1 or 2 replaces its saved pick; every other saved pick is kept as it is,
        /// even one the digits cannot show (a team already knocked out, or a game outside the pool). Saved picks also
        /// fill playoff slots, so a feeder outside the pool still counts.
        /// </param>
        public static SequenceResult Apply(IReadOnlyList<BowlGame> ordered, IReadOnlyList<BowlGame> allGames, string? typed,
            IReadOnlyDictionary<string, string>? stored = null)
        {
            var sequence = Normalize(typed, ordered.Count);
            var picks = stored == null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(stored, StringComparer.Ordinal);
            for (int i = 0; i < sequence.Length; i++)
                if (sequence[i] != '0') picks.Remove(ordered[i].Id);
            var keyed = new HashSet<string>(StringComparer.Ordinal);

            // A slot is known only once its feeder is picked, and a feeder may come later in the order,
            // so go over the digits until nothing more can be picked.
            bool changed = true;
            for (int pass = 0; changed && pass <= ordered.Count; pass++)
            {
                changed = false;
                for (int i = 0; i < sequence.Length; i++)
                {
                    var game = ordered[i];
                    if (sequence[i] == '0' || keyed.Contains(game.Id)) continue;

                    var matchup = EntryPickRules.Resolve(allGames, picks)[game.Id];
                    var (first, second) = Lines(game, matchup);
                    var team = sequence[i] == '1' ? first : second;
                    if (team == null) continue;

                    picks[game.Id] = team;
                    keyed.Add(game.Id);
                    changed = true;
                }
            }

            // Each game is keyed once and a slot only ever fills, so the keyed picks stay reachable; saved picks are
            // not cleared here (that is the admin's call, game by game).
            var notKnown = Enumerable.Range(0, sequence.Length)
                .Where(i => sequence[i] != '0' && !keyed.Contains(ordered[i].Id))
                .Select(i => ordered[i].Id)
                .ToList();

            return new SequenceResult(picks, sequence, notKnown);
        }

        /// <summary>
        /// When the keyer changes a game's digit, a later pick whose team came through that game no longer means what was
        /// keyed: its digit goes back to 0 (a saved pick it made unreachable is listed), so the page says which games to
        /// key again, the same as My Picks (issue 128). A pick that came from another game is kept. A digit that had no
        /// team yet (its feeder was blank) loses nothing and is kept. A game that changed in the same edit is left alone.
        /// </summary>
        public static RekeyResult ClearDependents(IReadOnlyList<BowlGame> ordered, IReadOnlyList<BowlGame> allGames,
            string? oldSequence, string? newSequence, IReadOnlyDictionary<string, string>? stored)
        {
            var before = Normalize(oldSequence, ordered.Count);
            var typedLength = Normalize(newSequence, ordered.Count).Length;
            var after = Normalize(newSequence, ordered.Count).PadRight(before.Length, '0').ToCharArray();
            char At(string s, int i) => i < s.Length ? s[i] : '0';
            char AtNew(int i) => i < after.Length ? after[i] : '0';

            var saved = stored == null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(stored, StringComparer.Ordinal);
            var beforePicks = Apply(ordered, allGames, before, saved).Picks;
            var beforeMatchups = EntryPickRules.Resolve(allGames, beforePicks);
            var editedIds = Enumerable.Range(0, ordered.Count).Where(i => At(before, i) != AtNew(i))
                .Select(i => ordered[i].Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var cleared = new List<string>();
            // Clearing one pick can change the team of a pick after it, so repeat until nothing more is cleared.
            bool changed = true;
            while (changed)
            {
                changed = false;
                var afterPicks = Apply(ordered, allGames, new string(after), saved).Picks;
                var afterMatchups = EntryPickRules.Resolve(allGames, afterPicks);

                for (int i = 0; i < ordered.Count; i++)
                {
                    var id = ordered[i].Id;
                    if (editedIds.Contains(id) || cleared.Contains(id)) continue;

                    if (AtNew(i) != '0')
                    {
                        // A keyed digit: cleared when it named a team before and now names another one (or none).
                        if (!beforePicks.TryGetValue(id, out var was)) continue;
                        afterPicks.TryGetValue(id, out var now);
                        if (string.Equals(was, now, StringComparison.OrdinalIgnoreCase)) continue;
                        after[i] = '0';
                    }
                    else
                    {
                        // A saved pick the digits leave alone: listed when it was reachable before and is not now.
                        if (!saved.TryGetValue(id, out var pick) || string.IsNullOrWhiteSpace(pick)) continue;
                        bool validBefore = beforeMatchups.TryGetValue(id, out var mb) && EntryPickRules.IsValidPick(mb, pick);
                        bool validAfter = afterMatchups.TryGetValue(id, out var ma) && EntryPickRules.IsValidPick(ma, pick);
                        if (!validBefore || validAfter) continue;
                        saved.Remove(id);
                    }

                    cleared.Add(id);
                    changed = true;
                }
            }

            // The keyer's line keeps the length they typed; a cleared digit shows as 0 where it was.
            var order = ordered.Select(g => g.Id).ToList();
            return new RekeyResult(new string(after, 0, typedLength), cleared.OrderBy(id => order.IndexOf(id)).ToList());
        }

        /// <summary>The digits for picks already made (an entry opened for editing), trailing blanks left off.</summary>
        public static string SequenceFor(IReadOnlyList<BowlGame> ordered, IReadOnlyList<BowlGame> allGames,
            IReadOnlyDictionary<string, string>? picks)
        {
            if (picks == null || picks.Count == 0) return string.Empty;

            var matchups = EntryPickRules.Resolve(allGames, picks);
            var digits = ordered.Select(g =>
            {
                if (!picks.TryGetValue(g.Id, out var pick) || !matchups.TryGetValue(g.Id, out var m)) return '0';
                var (first, second) = Lines(g, m);
                if (first != null && string.Equals(first, pick, StringComparison.OrdinalIgnoreCase)) return '1';
                if (second != null && string.Equals(second, pick, StringComparison.OrdinalIgnoreCase)) return '2';
                return '0';
            }).ToArray();

            return new string(digits).TrimEnd('0');
        }

        /// <summary>The game's teams in the order the printed form lists them: digit 1 is First, digit 2 is Second.</summary>
        public static (string? First, string? Second) Lines(BowlGame game, Matchup matchup) =>
            BlankFormSlots.HomeOnTop(game) ? (matchup.Home, matchup.Away) : (matchup.Away, matchup.Home);

        /// <summary>The game's team labels in the order the printed form lists them.</summary>
        public static (string First, string Second) LineLabels(BowlGame game, Matchup matchup) =>
            BlankFormSlots.HomeOnTop(game) ? (matchup.HomeLabel, matchup.AwayLabel) : (matchup.AwayLabel, matchup.HomeLabel);

        /// <summary>The sequence with one game's digit set (a team clicked in a row), padded with 0 when needed.</summary>
        public static string WithDigit(string? sequence, int index, char digit)
        {
            var chars = (sequence ?? string.Empty).PadRight(index + 1, '0').ToCharArray();
            chars[index] = digit;
            return new string(chars);
        }

        /// <summary>The games with no valid pick, in entry order: the check before save.</summary>
        public static List<BowlGame> Missing(IReadOnlyList<BowlGame> ordered, IReadOnlyList<BowlGame> allGames,
            IReadOnlyDictionary<string, string>? picks)
        {
            var matchups = EntryPickRules.Resolve(allGames, picks);
            return ordered
                .Where(g => picks == null
                    || !picks.TryGetValue(g.Id, out var pick)
                    || !matchups.TryGetValue(g.Id, out var m)
                    || !EntryPickRules.IsValidPick(m, pick))
                .ToList();
        }

        private static string Normalize(string? typed, int max)
        {
            if (string.IsNullOrEmpty(typed)) return string.Empty;
            var kept = typed
                .Select(c => c == '-' || c == ' ' ? '0' : c)
                .Where(c => c == '0' || c == '1' || c == '2')
                .Take(max)
                .ToArray();
            return new string(kept);
        }
    }
}
