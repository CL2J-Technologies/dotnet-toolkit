using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace cl2j.Logging
{
    [DebuggerStepThrough]
    internal class Logger : ILogger
    {
        private readonly string categoryName;
        private readonly Dictionary<string, LogLevel> filters;
        private readonly IDateTimeProvider dateTimeProvider;
        private readonly LoggerProvider provider;

        private readonly Dictionary<LogLevel, string> logLevelDescriptions = new()
        {
            { LogLevel.Critical, "CRI" },
            { LogLevel.Error, "ERR" },
            { LogLevel.Warning, "WRN" },
            { LogLevel.Information, "INF" },
            { LogLevel.Trace, "TRC" },
            { LogLevel.Debug, "DBG" },
        };

        private readonly Dictionary<LogLevel, ConsoleColor> logLevelColors = new()
        {
            { LogLevel.Critical, ConsoleColor.Red },
            { LogLevel.Error, ConsoleColor.Red },
            { LogLevel.Warning, ConsoleColor.DarkYellow },
            { LogLevel.Information, ConsoleColor.Cyan },
            { LogLevel.Debug, ConsoleColor.White },
            { LogLevel.Trace, ConsoleColor.DarkGray },
            { LogLevel.None, ConsoleColor.White }
        };

        public Logger(LoggerProvider provider, string categoryName, Dictionary<string, LogLevel> filters, IDateTimeProvider dateTimeProvider)
        {
            this.categoryName = categoryName;
            this.filters = filters;
            this.dateTimeProvider = dateTimeProvider;
            this.provider = provider;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
#pragma warning disable CS8603 // Possible null reference return.
            return null;
#pragma warning restore CS8603 // Possible null reference return.
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel < LogLevel.None;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            if (filters != null)
            {
                foreach (var kvp in filters)
                {
                    if (categoryName.StartsWith(kvp.Key, StringComparison.InvariantCulture) && logLevel < kvp.Value)
                        return;
                }
            }

            var sb = new StringBuilder();
            sb.Append($"{dateTimeProvider.Now():yyyy-MM-dd HH:mm:ss.fff} {logLevelDescriptions[logLevel]} [{categoryName}] ");
            var text = formatter(state, exception);
            sb.AppendLine(text);

            if (exception != null)
                sb.AppendLine(exception.ToString());

#if DEBUG
            Debug.Write(sb.ToString());
#endif

            if (provider.WriteToConsole)
            {
                Console.ForegroundColor = logLevelColors[logLevel];
                Console.Write(sb.ToString());
                Console.ForegroundColor = ConsoleColor.White;
            }

            provider.BufferedFile.AppendAsync(sb.ToString()).Wait();

            provider.MemoryLogger?.Log(dateTimeProvider.Now(), logLevel, exception, text);
        }
    }
}