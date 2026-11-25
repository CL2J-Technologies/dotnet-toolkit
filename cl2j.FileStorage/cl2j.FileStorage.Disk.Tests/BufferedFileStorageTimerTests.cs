using cl2j.FileStorage.Extensions;
using cl2j.FileStorage.Provider.Disk;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace cl2j.FileStorage.Disk.Tests
{
    /// <summary>
    /// Test of instance independence.
    ///
    /// The timer that triggers the periodic flush was held in a `static` field. Two instances
    /// therefore shared the same one: the second created orphaned the timer of the first, and
    /// `Dispose` released the one belonging to the last created, not its own. **Disposing one
    /// instance stopped the periodic flush of another**, with no exception and no trace — the
    /// buffer kept filling and nothing reached the disk any more.
    ///
    /// The defect had no effect in production: an application has a single `LoggerProvider`, and
    /// therefore a single `BufferedFileStorage`. Nothing enforced that, though, and a second
    /// instance would have produced a silent loss of log — the most expensive family of failures
    /// in this repository.
    ///
    /// ⚠️ **This test depends on time**, unlike the others in the project: the behaviour under
    /// test is that of a timer. It waits for a flush by polling the file, with a generous deadline
    /// so it does not become flaky on a loaded machine.
    /// </summary>
    public sealed class BufferedFileStorageTimerTests : IDisposable
    {
        private const int MaxSize = 1024 * 1024;
        private static readonly TimeSpan Cadence = TimeSpan.FromMilliseconds(100);
        private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);

        private readonly string root;
        private readonly FileStorageProviderDisk provider;

        public BufferedFileStorageTimerTests()
        {
            root = Path.Combine(Path.GetTempPath(), "cl2j-timer-" + Guid.NewGuid().ToString("N"));
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

        [Fact]
        public async Task Disposing_one_instance_does_not_stop_the_flush_of_another()
        {
            var first = new BufferedFileStorage(provider, "first_{0:yyyyMMdd}_{1:00}.log", MaxSize, Cadence, clearFile: true);
            var second = new BufferedFileStorage(provider, "second_{0:yyyyMMdd}_{1:00}.log", MaxSize, Cadence, clearFile: true);

            // With the static field, this released the timer belonging to `second`.
            first.Dispose();

            await second.AppendAsync("written by the timer" + Environment.NewLine);

            // Deliberately without `FlushAsync`: it is the **periodic** flush under test. An
            // explicit call would make the test pass on the broken code as well as the fixed one.
            var path = Path.Combine(root, second.CurrentFileName);
            var flushed = await WaitForContentAsync(path, "written by the timer");

            second.Dispose();

            Assert.True(flushed, $"The timer of the second instance flushed nothing into '{second.CurrentFileName}' in {Deadline.TotalSeconds} s.");
        }

        private static async Task<bool> WaitForContentAsync(string path, string expected)
        {
            var limite = DateTime.UtcNow.Add(Deadline);
            while (DateTime.UtcNow < limite)
            {
                if (File.Exists(path))
                {
                    // Shared read: the provider may write while we are looking.
                    using var flux = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var lecteur = new StreamReader(flux);
                    if ((await lecteur.ReadToEndAsync()).Contains(expected, StringComparison.Ordinal))
                        return true;
                }

                await Task.Delay(50);
            }

            return false;
        }
    }
}
