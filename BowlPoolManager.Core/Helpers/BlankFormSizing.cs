namespace BowlPoolManager.Core.Helpers
{
    /// <summary>Sizes for the blank entry form, which always fits one landscape letter page.</summary>
    public static class BlankFormSizing
    {
        private const int Columns = 3;
        private const double BowlAreaPx = 410;   // height left for the bowl grid on the page

        public static int Rows(int bowlCount) => Math.Max(1, (int)Math.Ceiling(bowlCount / (double)Columns));

        /// <summary>Team-name size in px: 11 for a normal season, smaller as rows are added, never below 7.</summary>
        public static int TeamFontPx(int rows)
        {
            var slot = BowlAreaPx / Math.Max(1, rows);
            return (int)Math.Clamp(Math.Floor((slot - 4) / 3.5), 7, 11);
        }
    }
}
