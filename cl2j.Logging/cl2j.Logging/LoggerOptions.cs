using Microsoft.Extensions.Logging;

namespace cl2j.Logging
{
    public class LoggerOptions
    {
        public string MainFileNamePattern { get; set; } = null!;
        public string TimeZoneName { get; set; } = null!;
        public int MaxSize { get; set; } = 3 * 1024 * 1024; //3 MB DEFAULT
        public TimeSpan FlushInterval { get; set; } = TimeSpan.FromSeconds(5);
        public bool IncludeConsole { get; set; }
        public bool ClearOnStartup { get; set; }
        public bool MemoryLogs { get; set; }
        public Dictionary<string, LogLevel> Filters { get; set; } = [];
    }
}