using System.Diagnostics;

namespace Microsoft.Extensions.Logging
{
    public enum BlockOutput
    {
        End,
        StartAndEnd
    }

    public static class LoggerBlockExtensions
    {
        public static LoggerBlock CreateBlockTrace(this ILogger logger, string state, BlockOutput output = BlockOutput.StartAndEnd)
        {
            return new LoggerBlock(logger, state, LogLevel.Trace, output);
        }

        public static LoggerBlock CreateBlockDebug(this ILogger logger, string state, BlockOutput output = BlockOutput.StartAndEnd)
        {
            return new LoggerBlock(logger, state, LogLevel.Debug, output);
        }

        public static LoggerBlock CreateBlockInfo(this ILogger logger, string state, BlockOutput output = BlockOutput.StartAndEnd)
        {
            return new LoggerBlock(logger, state, LogLevel.Information, output);
        }
    }

    public class LoggerBlock : IDisposable
    {
        private readonly Stopwatch stopwatch = new();
        private readonly ILogger logger;
        private readonly string state;
        private readonly LogLevel logLevel;

        public LoggerBlock(ILogger logger, string state, LogLevel logLevel, BlockOutput output)
        {
            this.logger = logger;
            this.state = state;
            this.logLevel = logLevel;

            if (output == BlockOutput.StartAndEnd)
                logger.Log(logLevel, 0, state, null, (s, exception) => state.ToString());

            stopwatch.Start();
        }

        public long ElapsedMilliseconds => stopwatch.ElapsedMilliseconds;

        public string EndState { get; set; } = null!;

        void IDisposable.Dispose()
        {
            stopwatch.Stop();

            var message = $"{state} {(EndState == null ? string.Empty : "--> " + EndState)} [{ElapsedTimeFormatted}]";
            logger.Log(logLevel, 0, message, null, (state, exception) => message);

            GC.SuppressFinalize(this);
        }

        private string ElapsedTimeFormatted => $"{stopwatch.Elapsed.Hours:00}:{stopwatch.Elapsed.Minutes:00}:{stopwatch.Elapsed.Seconds:00}.{stopwatch.Elapsed.Milliseconds:000}";
    }
}