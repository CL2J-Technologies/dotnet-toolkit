using cl2j.FileStorage.Core;
using cl2j.FileStorage.Provider.Disk;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace cl2j.Logging.Tests
{
    /// <summary>
    /// A unique temporary directory per test, a disk storage over it, and a factory that hands
    /// that storage to <see cref="LoggerProvider"/> — everything a test needs to read back what
    /// was written, with nothing left behind.
    ///
    /// <para>
    /// Reads go through <see cref="ReadAllLogsAsync"/> and <see cref="LogFilesAsync"/>, which first
    /// wait for every append the storage has started. Without that, a test is at the mercy of the
    /// flush timer: it ticks once immediately after construction, and a tick that picks up the
    /// buffer makes a concurrent flush return before the data has reached the disk.
    /// </para>
    /// </summary>
    internal sealed class LoggingTestHarness : IDisposable
    {
        public const string StorageName = "Logger";

        public LoggingTestHarness()
        {
            Root = Path.Combine(Path.GetTempPath(), "cl2j-logging-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Disk:Path"] = Root })
                .Build();

            var disk = new FileStorageProviderDisk();
            disk.Initialize(StorageName, configuration.GetSection("Disk"));
            Storage = new TrackingStorage(disk);
            Factory = new SingleStorageFactory(Storage);
        }

        public string Root { get; }

        public IFileStorageProvider Storage { get; }

        public IFileStorageFactory Factory { get; }

        public static LoggerOptions Options(Action<LoggerOptions>? configure = null)
        {
            var options = new LoggerOptions
            {
                MainFileNamePattern = "app_{0:yyyyMMdd}_{1:00}.log",
                TimeZoneName = "UTC",
                // Only a flush the test asks for, or the provider's disposal, should write.
                FlushInterval = TimeSpan.FromHours(1),
            };
            configure?.Invoke(options);
            return options;
        }

        public LoggerProvider CreateProvider(Action<LoggerOptions>? configure = null) =>
            new(Factory, Microsoft.Extensions.Options.Options.Create(Options(configure)));

        public async Task<string[]> LogFilesAsync(int expectedAppends = 1)
        {
            await ((TrackingStorage)Storage).AppendsCompletedAsync(expectedAppends);
            return Directory.GetFiles(Root, "*.log");
        }

        public async Task<string> ReadAllLogsAsync(int expectedAppends = 1) =>
            string.Concat((await LogFilesAsync(expectedAppends)).Order().Select(File.ReadAllText));

        public void Dispose()
        {
            ((TrackingStorage)Storage).AppendsCompletedAsync(0).GetAwaiter().GetResult();

            // Windows can hold a just-written file for a few milliseconds after its last handle
            // is closed (antivirus, indexer), so the delete is retried. This is cleanup, not an
            // assertion: nothing a test checks depends on the timing.
            for (var attempt = 0; Directory.Exists(Root); attempt++)
            {
                try
                {
                    Directory.Delete(Root, true);
                }
                catch (IOException) when (attempt < 40)
                {
                    Thread.Sleep(25);
                }
            }
        }

        private sealed class SingleStorageFactory(IFileStorageProvider storage) : IFileStorageFactory
        {
            public void Register<T>(string type) where T : IFileStorageProvider, new() => throw new NotSupportedException();

            public IFileStorageProvider GetProvider(string name) => storage;
        }

        private sealed class TrackingStorage(IFileStorageProvider inner) : IFileStorageProvider
        {
            private readonly List<Task> appends = [];

            public string Name { get => inner.Name; set => inner.Name = value; }

            private TaskCompletionSource appended = new(TaskCreationOptions.RunContinuationsAsynchronously);

            /// <summary>
            /// Waits until at least <paramref name="expected"/> appends have been started — by the
            /// test's own flush or by the timer's — then until every one of them has finished. The
            /// wait is on an event, not on the clock; the timeout only turns a hang into a failure.
            /// </summary>
            public async Task AppendsCompletedAsync(int expected)
            {
                while (true)
                {
                    Task signal;
                    lock (appends)
                    {
                        if (appends.Count >= expected)
                            break;
                        signal = appended.Task;
                    }

                    if (await Task.WhenAny(signal, Task.Delay(TimeSpan.FromSeconds(30))) != signal)
                        throw new TimeoutException($"Expected {expected} append(s), saw {appends.Count}.");
                }

                Task[] started;
                lock (appends)
                    started = [.. appends];
                await Task.WhenAll(started);
            }

            public Task AppendAsync(string name, Stream stream)
            {
                var append = inner.AppendAsync(name, stream);
                lock (appends)
                {
                    appends.Add(append);
                    appended.SetResult();
                    appended = new(TaskCreationOptions.RunContinuationsAsynchronously);
                }
                return append;
            }

            public void Initialize(string providerName, IConfigurationSection configuration) => inner.Initialize(providerName, configuration);

            public Task<bool> ExistsAsync(string name) => inner.ExistsAsync(name);

            public Task<FileStoreFileInfo?> GetInfoAsync(string name) => inner.GetInfoAsync(name);

            public Task<IEnumerable<string>> ListFilesAsync(string path) => inner.ListFilesAsync(path);

            public Task<IEnumerable<string>> ListFoldersAsync(string path) => inner.ListFoldersAsync(path);

            public Task<bool> ReadAsync(string name, Stream stream) => inner.ReadAsync(name, stream);

            public Task WriteAsync(string name, Stream stream, string? contentType = null) => inner.WriteAsync(name, stream, contentType);

            public Task DeleteAsync(string name) => inner.DeleteAsync(name);
        }
    }
}
