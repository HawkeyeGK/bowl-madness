namespace BowlPoolManager.Core.Helpers
{
    /// <summary>
    /// Which sports' sections the admin dashboard shows (Release 2 group E, owner decision 2026-11-28).
    /// On bowl-madness only football, on hoops-madness only hoops (the same host test as SiteContext.ActiveSport).
    /// Any other host (the test site, PR previews, the azurestaticapps.net address, localhost) shows both,
    /// so both sports' admin pages stay reachable where there is no sport domain.
    /// </summary>
    public sealed record DashboardSports(bool Football, bool Hoops)
    {
        /// <param name="host">The browser's host name, for example "bowl-madness.com".</param>
        public static DashboardSports ForHost(string? host)
        {
            var h = (host ?? "").ToLowerInvariant();
            if (h.Contains("hoops-madness")) return new DashboardSports(Football: false, Hoops: true);
            if (h.Contains("bowl-madness")) return new DashboardSports(Football: true, Hoops: false);
            return new DashboardSports(Football: true, Hoops: true);
        }
    }
}
