using System.Diagnostics;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.Logging
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    public enum BlockOutput
    {
        End,
        StartAndEnd
    }

    public static class LoggerBlockExtensions
    {
        public static LoggerBlock CreateBlockTrace(this ILogger logger, string state, BlockOutput output = BlockOutput.StartAndEnd, ProgressOptions? progressOptions = null)
        {
            return new LoggerBlock(logger, state, LogLevel.Trace, output, progressOptions);
        }

        public static LoggerBlock CreateBlockDebug(this ILogger logger, string state, BlockOutput output = BlockOutput.StartAndEnd, ProgressOptions? progressOptions = null)
        {
            return new LoggerBlock(logger, state, LogLevel.Debug, output, progressOptions);
        }

        public static LoggerBlock CreateBlockInfo(this ILogger logger, string state, BlockOutput output = BlockOutput.StartAndEnd, ProgressOptions? progressOptions = null)
        {
            return new LoggerBlock(logger, state, LogLevel.Information, output, progressOptions);
        }
    }

    public class ProgressOptions
    {
        public int Count { get; set; }
        public TimeSpan WriteFrequency { get; set; }

        public static ProgressOptions Create(int count)
        {
            return new ProgressOptions { Count = count, WriteFrequency = TimeSpan.FromSeconds(2) };
        }

        public static ProgressOptions Create(int count, TimeSpan writeFrequency)
        {
            return new ProgressOptions { Count = count, WriteFrequency = writeFrequency };
        }
    }

    public class LoggerBlock : IDisposable
    {
        private readonly Stopwatch stopwatch = new();
        private readonly ILogger logger;
        private readonly string state;
        private readonly LogLevel logLevel;

        private readonly ProgressOptions? progressOptions;
        private int step;
        private DateTime lastUpdate = DateTime.UtcNow;

        public LoggerBlock(ILogger logger, string state, LogLevel logLevel, BlockOutput output, ProgressOptions? progressOptions = null)
        {
            this.logger = logger;
            this.state = state;
            this.logLevel = logLevel;
            this.progressOptions = progressOptions;

            if (output == BlockOutput.StartAndEnd)
                logger.Log(logLevel, 0, state, null, (s, exception) => state.ToString());

            stopwatch.Start();
        }

        public long ElapsedMilliseconds => stopwatch.ElapsedMilliseconds;

        public string EndState { get; set; } = null!;

        public int Step { get { return step; } }

        void IDisposable.Dispose()
        {
            stopwatch.Stop();

            var message = $"{state} {(EndState == null ? string.Empty : "--> " + EndState)} [{ElapsedTimeFormatted}]";
            logger.Log(logLevel, 0, message, null, (state, exception) => message);

            GC.SuppressFinalize(this);
        }

        public void TraceTime(string? text)
        {
            if (logger.IsEnabled(LogLevel.Trace))
                logger.LogTrace($"{text} [{ElapsedTimeFormatted}]");
            stopwatch.Restart();
        }

        public void DebugTime(string? text)
        {
            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug($"{text} [{ElapsedTimeFormatted}]");
            stopwatch.Restart();
        }

        public void Increment(string? text = null)
        {
            if (progressOptions == null)
                return;

            ++step;
            if (progressOptions.Count > 0 && DateTime.UtcNow.Subtract(lastUpdate) > progressOptions.WriteFrequency)
            {
                var output = $"\t{step}/{progressOptions.Count}";
                if (text != null)
                    output += $" [{text}]";
                logger.Log(logLevel, 0, output);

                lastUpdate = DateTime.UtcNow;
            }
        }

        private string ElapsedTimeFormatted => $"{stopwatch.Elapsed.Hours:00}:{stopwatch.Elapsed.Minutes:00}:{stopwatch.Elapsed.Seconds:00}.{stopwatch.Elapsed.Milliseconds:000}";
    }
}