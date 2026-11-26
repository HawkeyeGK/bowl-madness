using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Core.Helpers
{
    public enum GridMark
    {
        /// <summary>Not decided yet, or no pick.</summary>
        None,
        /// <summary>The picked team won this game.</summary>
        Correct,
        /// <summary>The picked team has lost a game (this one or an earlier one), so the pick cannot score.</summary>
        Out
    }

    /// <summary>One cell of the all-picks print: the picked team's short name and its mark.</summary>
    public sealed record GridCell(string Text, GridMark Mark);

    /// <summary>
    /// The all-picks print (Release 2 group D): every entry's pick for every game of a pool, on one landscape page.
    /// Picks are short team names, marked once decided. Which picks a viewer may see is decided on the server
    /// (GetEntries hides other players' picks before lock); this only lays out what it is given.
    /// </summary>
    public sealed class AllPicksGrid
    {
        private const double PickAreaWidthPx = 810;   // page width left for the entry columns (CSS px): 10.5in less padding, game and winner columns
        private const double RowAreaHeightPx = 540;   // page height left for the game rows (CSS px)

        private readonly Dictionary<string, string> abbreviations;
        private readonly HashSet<string> losers;

        private AllPicksGrid(Dictionary<string, string> abbreviations, HashSet<string> losers)
        {
            this.abbreviations = abbreviations;
            this.losers = losers;
        }

        /// <param name="poolGames">Every game of the pool: abbreviations and losers are read from all of them.</param>
        public static AllPicksGrid For(IEnumerable<BowlGame> poolGames)
        {
            var abbreviations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var losers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var g in poolGames)
            {
                Remember(abbreviations, g.TeamHome, g.HomeTeamInfo);
                Remember(abbreviations, g.TeamAway, g.AwayTeamInfo);
                if (Decided(g) is { } result && !string.IsNullOrEmpty(result.Loser)) losers.Add(result.Loser);
            }
            return new AllPicksGrid(abbreviations, losers);
        }

        public GridCell Cell(BowlGame game, string? pick)
        {
            if (string.IsNullOrWhiteSpace(pick)) return new GridCell("", GridMark.None);

            var mark = string.Equals(Decided(game)?.Winner, pick, StringComparison.OrdinalIgnoreCase) ? GridMark.Correct
                     : losers.Contains(pick) ? GridMark.Out
                     : GridMark.None;
            return new GridCell(ShortName(pick), mark);
        }

        /// <summary>The team's abbreviation from the games' team data, else the first four letters of its name in capitals.</summary>
        public string ShortName(string team)
        {
            if (abbreviations.TryGetValue(team, out var abbreviation)) return abbreviation;
            var letters = new string(team.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
            return letters.Length <= 4 ? letters : letters[..4];
        }

        /// <summary>Entries in standings order (an entry with no rank goes last), then by name.</summary>
        public static List<BracketEntry> Order(IEnumerable<BracketEntry> entries, IReadOnlyDictionary<string, int> rankById) =>
            entries.OrderBy(e => rankById.TryGetValue(e.Id, out var rank) ? rank : int.MaxValue)
                   .ThenBy(e => e.PlayerName, StringComparer.OrdinalIgnoreCase)
                   .ToList();

        /// <summary>Pick text size in px: smaller as entries are added, never below 5 (one page always).</summary>
        public static int CellFontPx(int entries) =>
            (int)Math.Clamp(Math.Floor(PickAreaWidthPx / Math.Max(1, entries) / 3), 5, 10);

        /// <summary>Game row text size in px: smaller as games are added, never below 6.</summary>
        public static int RowFontPx(int games) =>
            (int)Math.Clamp(Math.Floor(RowAreaHeightPx / Math.Max(1, games) / 1.3), 6, 10);

        /// <summary>
        /// The winner and loser of a decided game, else null. Same rule as scoring (BUSINESS-RULES section 4):
        /// Final with both scores and no tie. BowlGame.WinningTeamName treats a missing score as 0, so it is not used here.
        /// </summary>
        public static (string? Winner, string? Loser)? Decided(BowlGame game)
        {
            if (game.Status != GameStatus.Final || game.TeamHomeScore is not int home || game.TeamAwayScore is not int away || home == away)
                return null;
            return home > away ? (game.TeamHome, game.TeamAway) : (game.TeamAway, game.TeamHome);
        }

        private static void Remember(Dictionary<string, string> map, string? team, TeamInfo? info)
        {
            if (string.IsNullOrEmpty(team) || string.IsNullOrWhiteSpace(info?.Abbreviation) || map.ContainsKey(team)) return;
            map[team] = info.Abbreviation;
        }
    }
}
