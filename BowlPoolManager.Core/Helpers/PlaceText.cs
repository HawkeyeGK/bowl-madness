namespace BowlPoolManager.Core.Helpers
{
    /// <summary>"2nd place" and "tied for 2nd place" for the pinned entry card.</summary>
    public static class PlaceText
    {
        public static string For(int rank, bool tied) =>
            tied ? $"tied for {Ordinal(rank)} place" : $"{Ordinal(rank)} place";

        private static string Ordinal(int n)
        {
            var lastTwo = n % 100;
            if (lastTwo is >= 11 and <= 13) return n + "th";
            return (n % 10) switch { 1 => n + "st", 2 => n + "nd", 3 => n + "rd", _ => n + "th" };
        }
    }
}
