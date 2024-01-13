using Microsoft.Extensions.Logging;

namespace cl2j.Logging
{
    public class LogData
    {
        public DateTimeOffset Stamp { get; set; }
        public LogLevel Level { get; set; }
        public string Text { get; set; } = "";
        public Exception? Exception { get; set; }
    }
}