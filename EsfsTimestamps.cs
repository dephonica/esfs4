using System;

namespace EsfsLite
{
    static class EsfsTimestamps
    {
        /// <summary>
        /// Converts a given DateTime into a Unix timestamp
        /// </summary>
        /// <param name="value">Any DateTime</param>
        /// <returns>The given DateTime in Unix timestamp format</returns>
        public static int ToUnixTimestamp(this DateTime value)
        {
            return (int)Math.Truncate((value.ToUniversalTime().Subtract(new DateTime(1970, 1, 1))).TotalSeconds);
        }

        /// <summary>
        /// Converts a given Unix timestamp into a DateTime
        /// </summary>
        /// <param name="value">Any Unix timestamp</param>
        /// <returns>The given Unix timestamp in DateTime format</returns>
        public static DateTime FromUnixTimestamp(this int value)
        {
            return (new DateTime(1970, 1, 1) + TimeSpan.FromSeconds(value)).ToLocalTime();
        }
    }
}
