namespace BowlPoolManager.Core.Helpers
{
    /// <summary>
    /// Splits standings rows into side-by-side blocks so the printout stays on one landscape page.
    /// Up to two blocks print at normal size. A longer pool prints compact (smaller type, more rows per block),
    /// because three normal-size blocks leave the names too little room.
    /// The "Coming up" panel takes the room of one block, so it shows only while the rows fit in two blocks.
    /// </summary>
    public sealed class StandingsPrintLayout<T>
    {
        public List<List<T>> Blocks { get; init; } = new();
        public bool Compact { get; init; }
        public bool HasRoomForPanel { get; init; }
    }

    public static class StandingsPrintLayout
    {
        public const int MaxRowsPerBlock = 28;
        public const int MaxCompactRowsPerBlock = 36;
        public const int MaxBlocksWithPanel = 2;

        public static StandingsPrintLayout<T> Split<T>(IReadOnlyList<T> rows)
        {
            bool compact = rows.Count > MaxBlocksWithPanel * MaxRowsPerBlock;
            int perBlock = compact ? MaxCompactRowsPerBlock : MaxRowsPerBlock;
            int blocks = Math.Max(1, (int)Math.Ceiling(rows.Count / (double)perBlock));
            int size = Math.Max(1, (int)Math.Ceiling(rows.Count / (double)blocks));
            var result = new List<List<T>>();
            for (int i = 0; i < blocks; i++)
                result.Add(rows.Skip(i * size).Take(size).ToList());
            return new StandingsPrintLayout<T> { Blocks = result, Compact = compact, HasRoomForPanel = blocks <= MaxBlocksWithPanel };
        }
    }
}
