using Microsoft.Extensions.Logging;

namespace cl2j.Database.Databases
{
    public class DatabaseOptions
    {
        public LogLevel TraceLevel { get; set; } = LogLevel.Trace;
        public LogLevel ExceptionLevel { get; set; } = LogLevel.Debug;

        public TimeSpan? BulkInsertTimeout { get; set; }
    }
}
