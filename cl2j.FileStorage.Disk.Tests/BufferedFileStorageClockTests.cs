using cl2j.FileStorage.Extensions;
using cl2j.FileStorage.Provider.Disk;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace cl2j.FileStorage.Disk.Tests
{
    /// <summary>
    /// Tests for the clock that names the files and decides when to roll over.
    ///
    /// They exist because of a defect seen in production on September 6th 2026: the log stamped
    /// its lines in the time zone the application configured while the file name and the rollover
    /// were hard-coded to UTC. The file `appartogocrawler_20260906_01.log` opened on a line dated
    /// the 5th at 19:59 — that is, midnight UTC. Diagnosing an evening therefore meant opening the
    /// next day file, with nothing to say so.
    ///
    /// What is tested here are the two observable consequences: the name follows the clock it is
    /// given, and so does the rollover. Both matter — fixing only one would make them diverge
    /// again, a file named for the right day still rolling over at the wrong moment.
    /// </summary>
    public sealed class BufferedFileStorageClockTests : IDisposable
    {
        private const string Pattern = "test_{0:yyyyMMdd}_{1:00}.log";
        private const int MaxSize = 1024 * 1024;

        private readonly string root;
        private readonly FileStorageProviderDisk provider;

        public BufferedFileStorageClockTests()
        {
            root = Path.Combine(Path.GetTempPath(), "cl2j-buffered-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Disk:Path"] = root })
                .Build();

            provider = new FileStorageProviderDisk();
            provider.Initialize("Disk", configuration.GetSection("Disk"));
        }

        public void Dispose()
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }

        // The moment of the defect: 20:30 on September 5th Eastern time, that is 00:30 on
        // September 6th UTC. The name must carry the 5th, like the lines it will contain.
        private static readonly DateTime SoireeLocale = new(2026, 9, 5, 20, 30, 0, DateTimeKind.Unspecified);

        [Fact]
        public void The_file_name_follows_the_clock_it_is_given()
        {
            using var buffered = new BufferedFileStorage(provider, Pattern, MaxSize, TimeSpan.FromHours(1), clearFile: true, () => SoireeLocale);

            Assert.Equal("test_20260905_01.log", buffered.CurrentFileName);
        }

        [Fact]
        public void Without_a_clock_the_name_stays_in_utc()
        {
            // The parameter is optional: callers that do not pass it keep the behaviour from
            // before the fix.
            using var buffered = new BufferedFileStorage(provider, Pattern, MaxSize, TimeSpan.FromHours(1), clearFile: true);

            Assert.Equal($"test_{DateTime.UtcNow:yyyyMMdd}_01.log", buffered.CurrentFileName);
        }

        [Fact]
        public async Task The_file_rollover_follows_the_clock_it_is_given()
        {
            var maintenant = SoireeLocale;

            using var buffered = new BufferedFileStorage(provider, Pattern, MaxSize, TimeSpan.FromHours(1), clearFile: true, () => maintenant);

            await buffered.AppendAsync("the evening of the 5th" + Environment.NewLine);
            await buffered.FlushAsync();
            Assert.Equal("test_20260905_01.log", buffered.CurrentFileName);

            // Local midnight crossed — three and a half hours after midnight UTC.
            maintenant = SoireeLocale.AddHours(4);

            await buffered.AppendAsync("the morning of the 6th" + Environment.NewLine);
            await buffered.FlushAsync();
            Assert.Equal("test_20260906_01.log", buffered.CurrentFileName);

            // And each line did go into the file for its own day.
            Assert.Contains("the evening of the 5th", await File.ReadAllTextAsync(Path.Combine(root, "test_20260905_01.log")), StringComparison.Ordinal);
            Assert.Contains("the morning of the 6th", await File.ReadAllTextAsync(Path.Combine(root, "test_20260906_01.log")), StringComparison.Ordinal);
        }

        [Fact]
        public async Task Midnight_utc_no_longer_triggers_a_rollover()
        {
            // The heart of the defect: at 19:00 then 21:00 local time, we cross midnight UTC
            // without crossing local midnight. Before the fix, two files were born here.
            var maintenant = new DateTime(2026, 9, 5, 19, 0, 0, DateTimeKind.Unspecified);

            using var buffered = new BufferedFileStorage(provider, Pattern, MaxSize, TimeSpan.FromHours(1), clearFile: true, () => maintenant);

            await buffered.AppendAsync("avant minuit UTC" + Environment.NewLine);
            await buffered.FlushAsync();

            maintenant = maintenant.AddHours(2);

            await buffered.AppendAsync("apres minuit UTC" + Environment.NewLine);
            await buffered.FlushAsync();

            Assert.Equal("test_20260905_01.log", buffered.CurrentFileName);
            Assert.Single(Directory.GetFiles(root, "test_*.log"));
        }
    }
}
