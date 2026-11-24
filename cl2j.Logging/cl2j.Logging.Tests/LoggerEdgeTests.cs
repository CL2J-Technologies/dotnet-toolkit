using Microsoft.Extensions.Logging;
using Xunit;

namespace cl2j.Logging.Tests
{
    /// <summary>
    ///     Console output swaps the process-wide <see cref="Console.Out"/>, so this class runs
    ///     alone: nothing else in the assembly writes to the console while it does.
    /// </summary>
    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class ConsoleGroup
    {
        public const string Name = "Console";
    }

    [Collection(ConsoleGroup.Name)]
    public sealed class LoggerEdgeTests : IDisposable
    {
        private readonly LoggingTestHarness harness = new();

        public void Dispose() => harness.Dispose();

        [Fact]
        public async Task A_line_is_echoed_to_the_console_when_the_options_ask_for_it()
        {
            var previous = Console.Out;
            using var captured = new StringWriter();
            try
            {
                Console.SetOut(captured);
                using var provider = harness.CreateProvider(o => o.IncludeConsole = true);
                provider.CreateLogger("Orders").LogWarning("order {Id} late", 7);
            }
            finally
            {
                Console.SetOut(previous);
            }

            Assert.Contains(" WRN [Orders] order 7 late", captured.ToString(), StringComparison.Ordinal);
            Assert.Contains(" WRN [Orders] order 7 late", await harness.ReadAllLogsAsync(), StringComparison.Ordinal);
        }

        [Fact]
        public async Task A_line_is_not_echoed_to_the_console_by_default()
        {
            var previous = Console.Out;
            using var captured = new StringWriter();
            try
            {
                Console.SetOut(captured);
                using var provider = harness.CreateProvider();
                provider.CreateLogger("Orders").LogWarning("order {Id} late", 7);
            }
            finally
            {
                Console.SetOut(previous);
            }

            Assert.Empty(captured.ToString());
            Assert.NotEmpty(await harness.ReadAllLogsAsync());
        }

        [Fact]
        public void A_block_reports_a_non_decreasing_elapsed_time()
        {
            using var provider = harness.CreateProvider();
            using var block = provider.CreateLogger("c").CreateBlockInfo("work");

            var first = block.ElapsedMilliseconds;
            var second = block.ElapsedMilliseconds;

            Assert.True(first >= 0);
            Assert.True(second >= first);
        }

        [Fact]
        public void A_scope_can_be_opened_and_closed_without_effect_on_the_line()
        {
            using var provider = harness.CreateProvider(o => o.MemoryLogs = true);
            var logger = provider.CreateLogger("c");

            using (logger.BeginScope("ignored"))
                logger.LogInformation("inside");

            Assert.Equal("inside", Assert.Single(provider.MemoryLogger.GetLogs()).Text);
        }
    }
}
