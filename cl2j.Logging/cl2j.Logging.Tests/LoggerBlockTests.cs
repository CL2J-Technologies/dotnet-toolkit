using Microsoft.Extensions.Logging;
using Xunit;

namespace cl2j.Logging.Tests
{
    internal sealed class CapturingLogger(LogLevel minimum = LogLevel.Trace) : ILogger
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= minimum;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }
    }

    public class LoggerBlockTests
    {
        private const string Elapsed = @"\[\d{2}:\d{2}:\d{2}\.\d{3}\]";

        [Fact]
        public void A_block_logs_its_start_and_its_end_with_the_elapsed_time()
        {
            var logger = new CapturingLogger();

            using (logger.CreateBlockInfo("import")) { }

            Assert.Equal(2, logger.Entries.Count);
            Assert.Equal((LogLevel.Information, "import"), logger.Entries[0]);
            Assert.Equal(LogLevel.Information, logger.Entries[1].Level);
            Assert.Matches($"^import  {Elapsed}$", logger.Entries[1].Message);
        }

        [Fact]
        public void A_block_created_with_End_only_logs_nothing_at_the_start()
        {
            var logger = new CapturingLogger();

            var block = logger.CreateBlockInfo("import", BlockOutput.End);

            Assert.Empty(logger.Entries);
            ((IDisposable)block).Dispose();
            Assert.Single(logger.Entries);
        }

        [Fact]
        public void The_end_state_is_written_after_an_arrow()
        {
            var logger = new CapturingLogger();

            using (var block = logger.CreateBlockInfo("import", BlockOutput.End))
                block.EndState = "12 rows";

            Assert.Matches($"^import --> 12 rows {Elapsed}$", logger.Entries.Single().Message);
        }

        [Theory]
        [InlineData(LogLevel.Trace)]
        [InlineData(LogLevel.Debug)]
        [InlineData(LogLevel.Information)]
        public void Each_factory_method_logs_at_its_level(LogLevel level)
        {
            var logger = new CapturingLogger();

            var block = level switch
            {
                LogLevel.Trace => logger.CreateBlockTrace("work"),
                LogLevel.Debug => logger.CreateBlockDebug("work"),
                _ => logger.CreateBlockInfo("work"),
            };
            ((IDisposable)block).Dispose();

            Assert.All(logger.Entries, e => Assert.Equal(level, e.Level));
            Assert.Equal(2, logger.Entries.Count);
        }

        [Fact]
        public void TraceTime_logs_the_text_with_the_elapsed_time_and_restarts_the_clock()
        {
            var logger = new CapturingLogger();
            using var block = logger.CreateBlockInfo("work", BlockOutput.End);

            block.TraceTime("step one");

            var entry = Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Trace, entry.Level);
            Assert.Matches($"^step one {Elapsed}$", entry.Message);
        }

        [Fact]
        public void DebugTime_logs_the_text_with_the_elapsed_time()
        {
            var logger = new CapturingLogger();
            using var block = logger.CreateBlockInfo("work", BlockOutput.End);

            block.DebugTime("step two");

            var entry = Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Debug, entry.Level);
            Assert.Matches($"^step two {Elapsed}$", entry.Message);
        }

        [Fact]
        public void TraceTime_and_DebugTime_log_nothing_when_their_level_is_disabled()
        {
            var logger = new CapturingLogger(LogLevel.Information);
            using var block = logger.CreateBlockInfo("work", BlockOutput.End);

            block.TraceTime("hidden");
            block.DebugTime("hidden");

            Assert.Empty(logger.Entries);
        }

        [Fact]
        public void Increment_does_nothing_without_progress_options()
        {
            var logger = new CapturingLogger();
            using var block = logger.CreateBlockInfo("work", BlockOutput.End);

            block.Increment();
            block.Increment("x");

            Assert.Equal(0, block.Step);
            Assert.Empty(logger.Entries);
        }

        [Fact]
        public void Increment_counts_steps_but_stays_quiet_inside_the_write_frequency()
        {
            var logger = new CapturingLogger();
            using var block = logger.CreateBlockInfo("work", BlockOutput.End, ProgressOptions.Create(10, TimeSpan.FromDays(1)));

            block.Increment();
            block.Increment();

            Assert.Equal(2, block.Step);
            Assert.Empty(logger.Entries);
        }

        [Fact]
        public void Increment_reports_the_step_out_of_the_total_once_the_write_frequency_has_elapsed()
        {
            var logger = new CapturingLogger();
            // A negative frequency has always elapsed: no waiting, no timing.
            using var block = logger.CreateBlockInfo("work", BlockOutput.End, ProgressOptions.Create(10, TimeSpan.FromTicks(-1)));

            block.Increment("item-a");

            var entry = Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Information, entry.Level);
            Assert.Equal("\t1/10 [item-a]", entry.Message);
        }

        [Fact]
        public void Increment_omits_the_brackets_when_there_is_no_text()
        {
            var logger = new CapturingLogger();
            using var block = logger.CreateBlockInfo("work", BlockOutput.End, ProgressOptions.Create(3, TimeSpan.FromTicks(-1)));

            block.Increment();

            Assert.Equal("\t1/3", logger.Entries.Single().Message);
        }

        [Fact]
        public void Increment_never_reports_when_the_total_is_zero()
        {
            var logger = new CapturingLogger();
            using var block = logger.CreateBlockInfo("work", BlockOutput.End, ProgressOptions.Create(0, TimeSpan.FromTicks(-1)));

            block.Increment();

            Assert.Empty(logger.Entries);
        }

        [Fact]
        public void ProgressOptions_Create_defaults_to_a_two_second_frequency()
        {
            var options = ProgressOptions.Create(5);

            Assert.Equal(5, options.Count);
            Assert.Equal(TimeSpan.FromSeconds(2), options.WriteFrequency);
        }
    }
}
