namespace BowlPoolManager.Core.Helpers
{
    /// <summary>Sizes for the printed entry form, which always fits one landscape letter page.</summary>
    public static class BlankFormSizing
    {
        private const int MaxRowsInThreeColumns = 9;
        private const double BowlAreaPx = 355;   // height left for the bowl grid on the page (CSS px)
        private const double FixedPxPerBowl = 11; // padding, border and the gap the team lines add beyond the type
        private const double LinesPerBowl = 3.5;  // bowl name plus two team lines, in units of the team-name size

        /// <summary>Three columns, or four when three would need more than nine rows.</summary>
        public static int Columns(int bowlCount) => bowlCount > 3 * MaxRowsInThreeColumns ? 4 : 3;

        public static int Rows(int bowlCount) => Math.Max(1, (int)Math.Ceiling(bowlCount / (double)Columns(bowlCount)));

        /// <summary>Bowl-name size in px (team names print 3 px larger): 11 for a short list, smaller as rows are added, never below 7.</summary>
        public static int BowlFontPx(int rows)
        {
            var slot = BowlAreaPx / Math.Max(1, rows);
            return (int)Math.Clamp(Math.Floor((slot - FixedPxPerBowl) / LinesPerBowl), 7, 11);
        }
    }
}
