using System;

namespace BowlPoolManager.Core.Helpers
{
    public static class DateTimeHelper
    {
        private static readonly TimeZoneInfo _centralTimeZone;

        static DateTimeHelper()
        {
            try
            {
                // Try IANA first (Standard for Linux/Mac/WASM)
                _centralTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");
            }
            catch
            {
                try
                {
                    // Fallback to Windows ID
                    _centralTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
                }
                catch
                {
                    // Final fallback to prevent crashes
                    Console.WriteLine("Warning: Could not find Central Time zone. Defaulting to Local.");
                    _centralTimeZone = TimeZoneInfo.Local;
                }
            }
        }

        public static DateTime ToCentral(DateTime utc)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(utc, _centralTimeZone);
        }

        /// <summary>
        /// The one way to show a stored time: Central, formatted, with " CT" after it (BUSINESS-RULES section 7).
        /// A value of unspecified kind is read as UTC, which is how the site stores times.
        /// </summary>
        public static string ToCentralLabel(DateTime utc, string format)
        {
            var asUtc = utc.Kind switch
            {
                DateTimeKind.Local => utc.ToUniversalTime(),
                _ => DateTime.SpecifyKind(utc, DateTimeKind.Utc)
            };
            return ToCentral(asUtc).ToString(format) + " CT";
        }

        /// <summary>Same, for a time that may be missing; <paramref name="whenNull"/> is shown then.</summary>
        public static string ToCentralLabel(DateTime? utc, string format, string whenNull = "") =>
            utc.HasValue ? ToCentralLabel(utc.Value, format) : whenNull;

        public static DateTime FromCentral(DateTime central)
        {
            // If the input is already UTC, just return it
            if (central.Kind == DateTimeKind.Utc) return central;
            
            // Treats Unspecified or Local as being in the Central Time Zone
            return TimeZoneInfo.ConvertTimeToUtc(central, _centralTimeZone);
        }

        /// <summary>
        /// Returns the Central Time Zone Info object if needed directly.
        /// </summary>
        public static TimeZoneInfo ZoneInfo => _centralTimeZone;
    }
}
