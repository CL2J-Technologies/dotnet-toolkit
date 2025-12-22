using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace cl2j.Tooling
{

    public class CacheLoader : IDisposable
    {
        private static readonly SemaphoreSlim semaphore = new(1, 1);
        private readonly string name;
        private readonly TimeSpan refreshInterval;
        private readonly Func<Task> refreshCallback;
        private readonly ILogger logger;
        private bool loaded;
        private Timer? timer;

        public CacheLoader(string name, TimeSpan refreshInterval, Func<Task> refreshCallback, ILogger logger)
        {
            this.name = name;
            this.refreshInterval = refreshInterval;
            this.refreshCallback = refreshCallback;
            this.logger = logger;

            timer = new Timer(RefreshAsync, null, TimeSpan.Zero, refreshInterval);
            logger.LogDebug($"CacheLoader<{name}> Initialized with refresh every {refreshInterval} [TimeCount={Timer.ActiveCount}]");
        }

        public async Task<bool> WaitAsync()
        {
            var sw = Stopwatch.StartNew();
            while (!loaded && sw.ElapsedMilliseconds <= refreshInterval.TotalMilliseconds)
                Thread.Sleep(100);

            await Task.CompletedTask;
            return loaded;
        }

        private async void RefreshAsync(object? state)
        {
            await semaphore.WaitAsync();
            try
            {
                await refreshCallback();
                loaded = true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"CacheLoader<{name}> : Unexpected error while doing the refresh.");
            }
            finally
            {
                semaphore.Release();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                timer?.Dispose();
                timer = null;
            }
        }
    }
}