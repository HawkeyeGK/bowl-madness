namespace BowlPoolManager.Core.Helpers
{
    /// <summary>
    /// Splits standings rows into side-by-side blocks so the printout stays on one landscape page.
    /// One block shows every column; several blocks show only the short columns.
    /// </summary>
    public sealed class StandingsPrintLayout<T>
    {
        public List<List<T>> Blocks { get; init; } = new();
        public bool ShowDetail { get; init; }
    }

    public static class StandingsPrintLayout
    {
        public const int MaxRowsPerBlock = 36;

        public static StandingsPrintLayout<T> Split<T>(IReadOnlyList<T> rows)
        {
            int blocks = Math.Max(1, (int)Math.Ceiling(rows.Count / (double)MaxRowsPerBlock));
            int size = Math.Max(1, (int)Math.Ceiling(rows.Count / (double)blocks));
            var result = new List<List<T>>();
            for (int i = 0; i < blocks; i++)
                result.Add(rows.Skip(i * size).Take(size).ToList());
            return new StandingsPrintLayout<T> { Blocks = result, ShowDetail = blocks == 1 };
        }
    }
}
