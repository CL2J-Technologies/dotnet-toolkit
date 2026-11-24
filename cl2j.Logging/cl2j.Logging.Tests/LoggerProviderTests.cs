using cl2j.FileStorage.Core;
using Microsoft.Extensions.Logging;
using Xunit;

namespace cl2j.Logging.Tests
{
    public sealed class LoggerProviderTests : IDisposable
    {
        private readonly LoggingTestHarness harness = new();

        public void Dispose() => harness.Dispose();

        [Fact]
        public async Task A_logged_line_has_the_timestamp_level_category_and_text()
        {
            using (var provider = harness.CreateProvider())
                provider.CreateLogger("Orders").LogInformation("order {Id} accepted", 42);

            var line = Assert.Single((await harness.ReadAllLogsAsync()).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
            Assert.Matches(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} INF \[Orders\] order 42 accepted$", line);
        }

        [Theory]
        [InlineData(LogLevel.Critical, "CRI")]
        [InlineData(LogLevel.Error, "ERR")]
        [InlineData(LogLevel.Warning, "WRN")]
        [InlineData(LogLevel.Information, "INF")]
        [InlineData(LogLevel.Debug, "DBG")]
        [InlineData(LogLevel.Trace, "TRC")]
        public async Task Each_level_has_its_own_three_letter_tag(LogLevel level, string tag)
        {
            using (var provider = harness.CreateProvider())
                provider.CreateLogger("c").Log(level, "message");

            Assert.Contains($" {tag} [c] message", await harness.ReadAllLogsAsync());
        }

        [Fact]
        public async Task The_level_None_writes_nothing()
        {
            using (var provider = harness.CreateProvider(o => o.MemoryLogs = true))
            {
                var logger = provider.CreateLogger("c");
                Assert.False(logger.IsEnabled(LogLevel.None));
                logger.Log(LogLevel.None, "ignored");

                Assert.Empty(provider.MemoryLogger.GetLogs());
            }

            Assert.Empty(await harness.LogFilesAsync(expectedAppends: 0));
        }

        [Fact]
        public void Every_other_level_is_enabled()
        {
            using var provider = harness.CreateProvider();
            var logger = provider.CreateLogger("c");

            foreach (var level in new[] { LogLevel.Trace, LogLevel.Debug, LogLevel.Information, LogLevel.Warning, LogLevel.Error, LogLevel.Critical })
                Assert.True(logger.IsEnabled(level), level.ToString());
        }

        [Fact]
        public async Task An_exception_is_written_after_the_message()
        {
            using (var provider = harness.CreateProvider())
                provider.CreateLogger("c").LogError(new InvalidOperationException("boom"), "failed");

            var text = await harness.ReadAllLogsAsync();
            Assert.Contains("ERR [c] failed", text);
            Assert.Contains("System.InvalidOperationException: boom", text);
            Assert.True(text.IndexOf("failed", StringComparison.Ordinal) < text.IndexOf("boom", StringComparison.Ordinal));
        }

        [Fact]
        public async Task An_explicit_flush_writes_what_is_buffered()
        {
            var provider = harness.CreateProvider();
            provider.CreateLogger("c").LogInformation("pending");

            await provider.BufferedFile.FlushAsync();
            Assert.Contains("pending", await harness.ReadAllLogsAsync());

            provider.Dispose();
        }

        [Fact]
        public async Task Disposing_the_provider_flushes_what_is_buffered()
        {
            var provider = harness.CreateProvider();
            provider.CreateLogger("c").LogInformation("last words");

            provider.Dispose();

            Assert.Contains("last words", await harness.ReadAllLogsAsync());
        }

        [Fact]
        public async Task Lines_from_several_categories_end_up_in_the_same_file_in_order()
        {
            using (var provider = harness.CreateProvider())
            {
                provider.CreateLogger("a").LogInformation("first");
                provider.CreateLogger("b").LogInformation("second");
            }

            var file = Assert.Single(await harness.LogFilesAsync());
            var lines = File.ReadAllLines(file);
            Assert.Equal(2, lines.Length);
            Assert.Contains("[a] first", lines[0]);
            Assert.Contains("[b] second", lines[1]);
        }

        [Fact]
        public async Task The_file_is_named_from_the_pattern_with_the_current_date_in_lower_case()
        {
            using (var provider = harness.CreateProvider(o => o.MainFileNamePattern = "MyApp_{0:yyyyMMdd}_{1:00}.LOG"))
                provider.CreateLogger("c").LogInformation("x");

            // The date is not asserted: it is the clock's, and a test must not race midnight.
            Assert.Matches(@"^myapp_\d{8}_01\.log$", Path.GetFileName(Assert.Single(await harness.LogFilesAsync())));
        }

        [Fact]
        public async Task ClearOnStartup_removes_the_file_left_by_a_previous_run()
        {
            using (var first = harness.CreateProvider())
                first.CreateLogger("c").LogInformation("previous run");

            using (var second = harness.CreateProvider(o => o.ClearOnStartup = true))
                second.CreateLogger("c").LogInformation("new run");

            var text = await harness.ReadAllLogsAsync(expectedAppends: 2);
            Assert.DoesNotContain("previous run", text);
            Assert.Contains("new run", text);
        }

        [Fact]
        public async Task Without_ClearOnStartup_a_new_run_appends_to_the_existing_file()
        {
            using (var first = harness.CreateProvider())
                first.CreateLogger("c").LogInformation("previous run");

            using (var second = harness.CreateProvider())
                second.CreateLogger("c").LogInformation("new run");

            var text = await harness.ReadAllLogsAsync(expectedAppends: 2);
            Assert.Contains("previous run", text);
            Assert.Contains("new run", text);
        }

        [Fact]
        public async Task A_file_that_reached_MaxSize_rolls_over_to_the_next_number()
        {
            using (var provider = harness.CreateProvider(o => o.MaxSize = 50))
            {
                var logger = provider.CreateLogger("c");
                logger.LogInformation("{Filler}", new string('x', 100));
                await provider.BufferedFile.FlushAsync();
                logger.LogInformation("second file");
            }

            var names = (await harness.LogFilesAsync(expectedAppends: 2)).Select(Path.GetFileName).Order().ToArray();
            Assert.Equal(2, names.Length);
            Assert.EndsWith("_01.log", names[0]);
            Assert.EndsWith("_02.log", names[1]);
            Assert.Contains("second file", File.ReadAllText(Path.Combine(harness.Root, names[1]!)));
            Assert.DoesNotContain("second file", File.ReadAllText(Path.Combine(harness.Root, names[0]!)));
        }

        [Fact]
        public void MemoryLogs_keeps_the_lines_in_memory_with_their_level_text_and_exception()
        {
            using var provider = harness.CreateProvider(o => o.MemoryLogs = true);
            var exception = new InvalidOperationException("boom");

            provider.CreateLogger("c").LogWarning(exception, "careful {What}", "now");

            var entry = Assert.Single(provider.MemoryLogger.GetLogs());
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Equal("careful now", entry.Text);
            Assert.Same(exception, entry.Exception);
        }

        [Fact]
        public void Without_MemoryLogs_nothing_is_kept_in_memory()
        {
            using var provider = harness.CreateProvider();

            provider.CreateLogger("c").LogInformation("x");

            Assert.Null(provider.MemoryLogger);
        }

        [Fact]
        public void ClearMemoryLogs_empties_the_memory_logs()
        {
            using var provider = harness.CreateProvider(o => o.MemoryLogs = true);
            provider.CreateLogger("c").LogInformation("x");

            provider.ClearMemoryLogs();

            Assert.Empty(provider.MemoryLogger.GetLogs());
        }

        [Fact]
        public void ClearMemoryLogs_does_nothing_without_MemoryLogs()
        {
            using var provider = harness.CreateProvider();

            provider.ClearMemoryLogs();
        }

        [Fact]
        public void The_memory_logs_keep_only_the_most_recent_1000_lines()
        {
            using var provider = harness.CreateProvider(o => o.MemoryLogs = true);
            var logger = provider.CreateLogger("c");

            for (var i = 0; i < 1005; i++)
                logger.LogInformation("line {I}", i);

            var logs = provider.MemoryLogger.GetLogs();
            Assert.Equal(1000, logs.Count);
            Assert.Equal("line 5", logs[0].Text);
            Assert.Equal("line 1004", logs[^1].Text);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(int.MinValue)]
        public void A_MemoryLogger_rejects_a_capacity_below_one(int maxLines)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MemoryLogger(maxLines));
        }

        [Fact]
        public void A_MemoryLogger_accepts_the_default_capacity()
        {
            Assert.Empty(new MemoryLogger().GetLogs());
        }

        [Fact]
        public void Disposing_the_provider_runs_Dispose_with_disposing_true()
        {
            var provider = new RecordingProvider(harness.Factory);

            provider.Dispose();

            Assert.Equal([true], provider.DisposeCalls);
        }

        [Fact]
        public async Task A_disposed_provider_has_flushed_its_lines()
        {
            var provider = harness.CreateProvider();
            provider.CreateLogger("c").LogInformation("flushed on dispose");

            provider.Dispose();

            Assert.Contains("flushed on dispose", await harness.ReadAllLogsAsync());
        }

        private sealed class RecordingProvider(IFileStorageFactory factory)
            : LoggerProvider(factory, Microsoft.Extensions.Options.Options.Create(LoggingTestHarness.Options()))
        {
            public List<bool> DisposeCalls { get; } = [];

            protected override void Dispose(bool disposing)
            {
                DisposeCalls.Add(disposing);
                base.Dispose(disposing);
            }
        }

        [Fact]
        public void WriteToConsole_follows_IncludeConsole()
        {
            using var quiet = harness.CreateProvider();
            using var loud = harness.CreateProvider(o => o.IncludeConsole = true);

            Assert.False(quiet.WriteToConsole);
            Assert.True(loud.WriteToConsole);
        }

        [Theory]
        [InlineData("UTC")]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Not/AZone")]
        public void The_clock_is_UTC_when_the_time_zone_is_utc_missing_or_unknown(string? timeZone)
        {
            using var provider = harness.CreateProvider(o =>
            {
                o.TimeZoneName = timeZone!;
                o.MemoryLogs = true;
            });

            provider.CreateLogger("c").LogInformation("x");

            var stamp = Assert.Single(provider.MemoryLogger.GetLogs()).Stamp;
            Assert.InRange((stamp.DateTime - DateTime.UtcNow).Duration(), TimeSpan.Zero, TimeSpan.FromMinutes(1));
        }

        [Fact]
        public void The_clock_follows_the_configured_time_zone()
        {
            const string timeZone = "Pacific/Auckland";
            using var provider = harness.CreateProvider(o =>
            {
                o.TimeZoneName = timeZone;
                o.MemoryLogs = true;
            });

            provider.CreateLogger("c").LogInformation("x");

            var expected = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(timeZone));
            var stamp = Assert.Single(provider.MemoryLogger.GetLogs()).Stamp;
            Assert.InRange((stamp.DateTime - expected).Duration(), TimeSpan.Zero, TimeSpan.FromMinutes(1));
        }
    }
}
