using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Core.Helpers
{
    /// <summary>One open pick that would close (or keep) the gap: the entry's team in a game where the other entry picked differently.</summary>
    /// <param name="Over">The other entry's team in that game when it is still alive; null when the other entry has no live pick there.</param>
    /// <param name="Text">"Georgia over Texas (Sugar Bowl, 6 pts)" or "Miami wins (Final, 10 pts)".</param>
    public sealed record SwingGame(string GameId, string BowlName, int Points, string Team, string? Over, string Text);

    /// <summary>One contender's line on the "Who can still win" view.</summary>
    public sealed class ContenderLine
    {
        public SimulatedRow Row { get; init; } = new();
        /// <summary>True for the entry in 1st place.</summary>
        public bool IsLeader { get; init; }
        /// <summary>
        /// Its score plus the points of its swing games (where it differs from the leader) reaches the leader's score,
        /// so 1st place is still possible on points. Always true for the leader. Approximate for linked playoff picks.
        /// </summary>
        public bool CanWin { get; init; }
        /// <summary>Whom the line compares with: the leader, or for the leader, 2nd place. Null when the pool has one entry.</summary>
        public string? ComparedWith { get; init; }
        /// <summary>"6 behind Adam", "Leads Sally by 6", "Level with Adam on points", or empty.</summary>
        public string GapText { get; init; } = "";
        /// <summary>Open picks that differ from <see cref="ComparedWith"/>, biggest points first, then by kickoff.</summary>
        public IReadOnlyList<SwingGame> Swings { get; init; } = Array.Empty<SwingGame>();
        /// <summary>The words before the swing games: "To stay ahead" for the leader, "Needs" for everyone else.</summary>
        public string SwingsLabel => IsLeader ? "To stay ahead" : "Needs";
        /// <summary>
        /// How many swing games the card shows before "+ N more": three, or all of them when only one more would be
        /// hidden (a button for one game is not worth a tap).
        /// </summary>
        public int ShownByDefault => Swings.Count <= PathToVictory.SwingsShown + 1 ? Swings.Count : PathToVictory.SwingsShown;
        /// <summary>The number behind "+ N more"; 0 when every swing game is shown.</summary>
        public int HiddenByDefault => Swings.Count - ShownByDefault;
        /// <summary>What to say when <see cref="Swings"/> is empty; empty when there is nobody to compare with.</summary>
        public string NoSwingsText { get; init; } = "";
    }

    public sealed class PathToVictoryView
    {
        /// <summary>Entries that can still finish in the top 3, in rank order.</summary>
        public IReadOnlyList<ContenderLine> Contenders { get; init; } = Array.Empty<ContenderLine>();
        /// <summary>Entries that can no longer finish in the top 3, in rank order.</summary>
        public IReadOnlyList<SimulatedRow> Out { get; init; } = Array.Empty<SimulatedRow>();
    }

    /// <summary>
    /// The default Path to Victory view, "Who can still win, and what has to happen" (Release 2 group E).
    /// It only reads the simulator's output: contenders are the rows that can still finish top 3 (the same rule as
    /// the leaderboard's "out" marker), and each line lists the open picks where the entry differs from the leader
    /// (for the leader, from 2nd place). No scoring happens here.
    /// </summary>
    public static class PathToVictory
    {
        /// <summary>Swing games a card shows before "+ N more" (owner decision 2026-11-28).</summary>
        public const int SwingsShown = 3;

        /// <summary>
        /// Whether Path to Victory lists a pool. It draws every entry's picks, so a pool shows only once it has locked
        /// (BUSINESS-RULES section 3, <see cref="PickVisibility"/>), and never once it is archived. Locked means after the
        /// lock date, as on the server (entries may still change up to and including it).
        /// </summary>
        public static bool ShowsPool(BowlPool pool, DateTime utcNow) =>
            !pool.IsArchived && PickVisibility.OnPublicPage(poolLocked: utcNow > pool.LockDate, isViewersOwnEntry: false);

        /// <param name="sim">The simulator's result for the pool (with any winners the viewer chose).</param>
        /// <param name="games">The pool's games (for names, points and kickoff).</param>
        public static PathToVictoryView Summarize(SimulationResult sim, IReadOnlyList<BowlGame> games)
        {
            var rows = sim.Rows;
            if (rows.Count == 0) return new PathToVictoryView();

            var leader = rows[0];
            var orderedGames = games
                .Where(g => g.PointValue > 0)
                .OrderByDescending(g => g.PointValue)
                .ThenBy(g => g.StartTime)
                .ThenBy(g => g.Id, StringComparer.Ordinal)
                .ToList();

            var contenders = rows
                .Where(r => r.CanFinishTop3)
                .Select(r => Line(r, ReferenceFor(r)))
                .ToList();

            return new PathToVictoryView
            {
                Contenders = contenders,
                Out = rows.Where(r => !r.CanFinishTop3).ToList()
            };

            // The leader is compared with the next entry; everyone else with the leader.
            SimulatedRow? ReferenceFor(SimulatedRow row) =>
                ReferenceEquals(row, leader) ? rows.Skip(1).FirstOrDefault() : leader;

            ContenderLine Line(SimulatedRow row, SimulatedRow? other)
            {
                var isLeader = ReferenceEquals(row, leader);
                if (other == null)
                    return new ContenderLine { Row = row, IsLeader = isLeader, CanWin = true };

                var otherName = other.Entry.PlayerName;
                var swings = Swings(row, other);
                return new ContenderLine
                {
                    Row = row,
                    IsLeader = isLeader,
                    // Points the leader also gets cannot close the gap, so only the differing games count
                    // (owner decision 2026-11-28). Everyone but the leader is compared with the leader.
                    CanWin = isLeader || row.Score + swings.Sum(s => s.Points) >= leader.Score,
                    ComparedWith = otherName,
                    GapText = GapText(row.Score, other.Score, otherName),
                    Swings = swings,
                    NoSwingsText = $"No games left where their picks differ from {otherName}'s."
                };
            }

            List<SwingGame> Swings(SimulatedRow row, SimulatedRow other)
            {
                var list = new List<SwingGame>();
                foreach (var game in orderedGames)
                {
                    if (!row.Picks.TryGetValue(game.Id, out var state) || state != PickState.Open) continue;
                    var team = row.Entry.Picks![game.Id];

                    string? over = null;
                    if (other.Picks.TryGetValue(game.Id, out var otherState) && otherState == PickState.Open)
                    {
                        var otherTeam = other.Entry.Picks![game.Id];
                        if (string.Equals(team, otherTeam, StringComparison.OrdinalIgnoreCase)) continue;
                        over = otherTeam;
                    }

                    var what = over != null ? $"{team} over {over}" : $"{team} wins";
                    list.Add(new SwingGame(game.Id, game.BowlName, game.PointValue, team, over,
                        $"{what} ({game.BowlName}, {PointsText(game.PointValue)})"));
                }
                return list;
            }
        }

        private static string GapText(int score, int otherScore, string otherName) =>
            score > otherScore ? $"Leads {otherName} by {score - otherScore}"
            : score < otherScore ? $"{otherScore - score} behind {otherName}"
            : $"Level with {otherName} on points";

        private static string PointsText(int points) => points == 1 ? "1 pt" : $"{points} pts";
    }
}
