using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace cl2j.Tooling
{
#pragma warning disable CA1001 // Types that own disposable fields should be disposable

    public class CacheLoader
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
    {
        private static readonly SemaphoreSlim semaphore = new(1, 1);
        private readonly string name;
        private readonly TimeSpan refreshInterval;
        private readonly Func<Task> refreshCallback;
        private readonly ILogger logger;
        private bool loaded;
#pragma warning disable IDE0052 // Remove unread private members
        private readonly Timer timer;
#pragma warning restore IDE0052 // Remove unread private members

        public CacheLoader(string name, TimeSpan refreshInterval, Func<Task> refreshCallback, ILogger logger)
        {
            this.name = name;
            this.refreshInterval = refreshInterval;
            this.refreshCallback = refreshCallback;
            this.logger = logger;

            logger.LogDebug($"CacheLoader<{name}> Initialized with refresh every {refreshInterval}");

            timer = new Timer(RefreshAsync, null, TimeSpan.Zero, refreshInterval);
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
    }
}