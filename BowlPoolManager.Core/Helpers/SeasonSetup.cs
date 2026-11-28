using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Core.Helpers
{
    /// <summary>The steps of setting up a football season, in the order an admin does them.</summary>
    public enum SetupStepKey
    {
        CreateSeason,
        LoadGames,
        LinkFeed,
        SetPoolGames,
        OpenPool,
        PrintBlankForms,
        KeyPaperEntries,
        MarkPayments
    }

    /// <summary>
    /// How far a step has got, as far as the stored data can tell. Info is for steps the data cannot
    /// call done (printing leaves no record; paper entries keep coming until lock).
    /// </summary>
    public enum SetupStepState
    {
        NotStarted,
        InProgress,
        Done,
        Info
    }

    /// <param name="Key">Which step.</param>
    /// <param name="Title">The step in a few words.</param>
    /// <param name="Href">The existing admin page where the step is done (relative, like the dashboard links).</param>
    /// <param name="LinkText">The button text for that page.</param>
    /// <param name="State">How far the step has got.</param>
    /// <param name="Detail">One line saying what the data shows.</param>
    public sealed record SetupStep(SetupStepKey Key, string Title, string Href, string LinkText, SetupStepState State, string Detail);

    /// <summary>
    /// The admin season setup checklist (Release 2 group E). Reads only data the page already has:
    /// the current season, its games, its pools and its entries. Archived pools and their entries are ignored.
    /// </summary>
    public static class SeasonSetup
    {
        /// <param name="season">The season SeasonService calls current (it falls back to the newest one when none is marked current).</param>
        /// <param name="games">The season's football games.</param>
        /// <param name="pools">The season's football pools, archived ones included or not.</param>
        /// <param name="entries">The season's football entries.</param>
        /// <param name="utcNow">The time now, in UTC, to tell open pools from locked ones.</param>
        public static IReadOnlyList<SetupStep> Build(Season? season, IReadOnlyList<BowlGame> games,
            IReadOnlyList<BowlPool> pools, IReadOnlyList<BracketEntry> entries, DateTime utcNow)
        {
            var activePools = pools.Where(p => !p.IsArchived).ToList();
            var activePoolIds = activePools.Select(p => p.Id).ToHashSet();
            var activeEntries = entries.Where(e => activePoolIds.Contains(e.PoolId)).ToList();

            return new[]
            {
                SeasonStep(season),
                GamesStep(games),
                FeedStep(games),
                PoolGamesStep(activePools),
                OpenPoolStep(activePools, utcNow),
                new SetupStep(SetupStepKey.PrintBlankForms, "Print blank forms", "print-bracket", "Blank form", SetupStepState.Info,
                    "Print each pool's blank form for players who enter on paper"),
                new SetupStep(SetupStepKey.KeyPaperEntries, "Key paper entries", "admin/entry", "New entry", SetupStepState.Info,
                    $"{Count(activeEntries.Count(e => e.IsAdminEntered), "entry", "entries")} keyed by an admin so far"),
                PaymentsStep(activeEntries)
            };
        }

        private static SetupStep SeasonStep(Season? season)
        {
            const string title = "Create the season";
            const string href = "admin/seasons";

            if (season == null)
                return new SetupStep(SetupStepKey.CreateSeason, title, href, "Seasons", SetupStepState.NotStarted, "No season yet");
            if (!season.IsCurrent)
                return new SetupStep(SetupStepKey.CreateSeason, title, href, "Seasons", SetupStepState.InProgress,
                    $"No season is marked current; showing {season.Name}");
            return new SetupStep(SetupStepKey.CreateSeason, title, href, "Seasons", SetupStepState.Done, $"{season.Name} is current");
        }

        private static SetupStep GamesStep(IReadOnlyList<BowlGame> games)
        {
            const string title = "Load the games";
            const string href = "admin/games";

            return games.Count == 0
                ? new SetupStep(SetupStepKey.LoadGames, title, href, "Games", SetupStepState.NotStarted, "No games yet")
                : new SetupStep(SetupStepKey.LoadGames, title, href, "Games", SetupStepState.Done, $"{Count(games.Count, "game", "games")} loaded");
        }

        private static SetupStep FeedStep(IReadOnlyList<BowlGame> games)
        {
            const string title = "Link the score feed";
            const string href = "admin/linker";

            if (games.Count == 0)
                return new SetupStep(SetupStepKey.LinkFeed, title, href, "Link games", SetupStepState.NotStarted, "Load the games first");

            var linked = games.Count(g => !string.IsNullOrWhiteSpace(g.ExternalId));
            if (linked == games.Count)
                return new SetupStep(SetupStepKey.LinkFeed, title, href, "Link games", SetupStepState.Done, $"All {Count(games.Count, "game", "games")} linked");

            var state = linked == 0 ? SetupStepState.NotStarted : SetupStepState.InProgress;
            return new SetupStep(SetupStepKey.LinkFeed, title, href, "Link games", state, $"{linked} of {Count(games.Count, "game", "games")} linked");
        }

        private static SetupStep PoolGamesStep(IReadOnlyList<BowlPool> pools)
        {
            const string title = "Set each pool's games";
            const string href = "admin/pools";

            if (pools.Count == 0)
                return new SetupStep(SetupStepKey.SetPoolGames, title, href, "Pools", SetupStepState.NotStarted, "No pool yet");

            var withoutGames = pools.Where(p => p.GameIds.Count == 0).Select(p => p.Name).ToList();
            if (withoutGames.Count == 0)
                return new SetupStep(SetupStepKey.SetPoolGames, title, href, "Pools", SetupStepState.Done,
                    pools.Count == 1 ? "The pool has its games" : $"{pools.Count} pools have their games");

            var state = withoutGames.Count == pools.Count ? SetupStepState.NotStarted : SetupStepState.InProgress;
            return new SetupStep(SetupStepKey.SetPoolGames, title, href, "Pools", state, "No games yet: " + string.Join(", ", withoutGames));
        }

        // A pool is open when it has games, is not concluded and its lock date has not passed (BUSINESS-RULES section 2:
        // Active up to and including the lock date). Once every pool with games has locked or concluded, opening is behind us.
        private static SetupStep OpenPoolStep(IReadOnlyList<BowlPool> pools, DateTime utcNow)
        {
            const string title = "Open the pool";
            const string href = "admin/pools";

            var ready = pools.Where(p => p.GameIds.Count > 0).ToList();
            if (ready.Count == 0)
                return new SetupStep(SetupStepKey.OpenPool, title, href, "Pools", SetupStepState.NotStarted, "Set a pool's games first");

            var open = ready.Where(p => !p.IsConcluded && p.LockDate >= utcNow).OrderBy(p => p.LockDate).ToList();
            if (open.Count == 0)
                return new SetupStep(SetupStepKey.OpenPool, title, href, "Pools", SetupStepState.Done, "No pool is open: each has locked or is concluded");

            var lines = open.Select(p =>
                $"{p.Name} (code {p.InviteCode}), locks {DateTimeHelper.ToCentralLabel(p.LockDate, "ddd MMM d, h:mm tt")}");
            return new SetupStep(SetupStepKey.OpenPool, title, href, "Pools", SetupStepState.Done, "Open: " + string.Join("; ", lines));
        }

        private static SetupStep PaymentsStep(IReadOnlyList<BracketEntry> entries)
        {
            const string title = "Mark payments";
            const string href = "admin/payments";

            if (entries.Count == 0)
                return new SetupStep(SetupStepKey.MarkPayments, title, href, "Payments", SetupStepState.NotStarted, "No entries yet");

            var paid = entries.Count(e => e.IsPaid);
            if (paid == entries.Count)
                return new SetupStep(SetupStepKey.MarkPayments, title, href, "Payments", SetupStepState.Done, $"All {Count(entries.Count, "entry", "entries")} paid");

            var state = paid == 0 ? SetupStepState.NotStarted : SetupStepState.InProgress;
            return new SetupStep(SetupStepKey.MarkPayments, title, href, "Payments", state, $"{paid} of {Count(entries.Count, "entry", "entries")} paid");
        }

        private static string Count(int n, string one, string many) => $"{n} {(n == 1 ? one : many)}";
    }
}
