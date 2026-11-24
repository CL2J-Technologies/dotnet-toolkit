using Microsoft.Extensions.Logging;

namespace cl2j.Database.Databases
{
    public class DatabaseOptions
    {
        public LogLevel TraceLevel { get; set; } = LogLevel.Trace;
        public LogLevel ExceptionLevel { get; set; } = LogLevel.Debug;

        public TimeSpan? BulkInsertTimeout { get; set; }

        /// <summary>
        ///     The bulk insert timeout in whole seconds, as a provider's bulk copy API expects it:
        ///     <see cref="BulkInsertTimeout"/> rounded up, or <paramref name="connectionTimeoutSeconds"/>
        ///     when it is not set. Zero means no limit.
        /// </summary>
        /// <remarks>
        ///     Uses <see cref="TimeSpan.TotalSeconds"/>. <see cref="TimeSpan.Seconds"/> is only the
        ///     seconds component (0 to 59), which turned a two-minute timeout into no limit at all and
        ///     a 90-second one into 30 seconds.
        /// </remarks>
        public int GetBulkInsertTimeoutSeconds(int connectionTimeoutSeconds)
        {
            if (BulkInsertTimeout is not { } timeout)
                return connectionTimeoutSeconds;

            var seconds = Math.Ceiling(timeout.TotalSeconds);
            return seconds >= int.MaxValue ? int.MaxValue : (int)seconds;
        }
    }
}
