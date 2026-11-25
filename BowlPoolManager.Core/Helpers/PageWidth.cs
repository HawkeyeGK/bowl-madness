namespace BowlPoolManager.Core.Helpers
{
    /// <summary>
    /// The site's one page-width rule (Release 2). Every page is as wide as the header (1240 px, centered) so moving
    /// between pages never jumps. Only big grids get the full screen: What If (football and hoops) and every admin page.
    /// The layout adds the wide class; pages do not choose their own width.
    /// </summary>
    public static class PageWidth
    {
        /// <param name="relativePath">The page address relative to the site root, for example "what-if" or "admin/users?x=1".</param>
        public static bool IsWide(string? relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) return false;

            var path = relativePath.Trim();
            var cut = path.IndexOfAny(new[] { '?', '#' });
            if (cut >= 0) path = path.Substring(0, cut);
            path = path.Trim('/').ToLowerInvariant();

            return path == "what-if"
                || path == "hoops/what-if"
                || path == "admin"
                || path.StartsWith("admin/", StringComparison.Ordinal);
        }
    }
}
