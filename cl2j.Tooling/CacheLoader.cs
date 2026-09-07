using Microsoft.Extensions.Logging;

namespace cl2j.Tooling
{
    /// <summary>
    ///     Keeps something loaded, reloading it on an interval, and lets a caller wait for the
    ///     first load to have happened.
    /// </summary>
    public class CacheLoader : IDisposable
    {
        /// <summary>
        ///     One per loader. It guards this loader's refreshes against each other — a refresh
        ///     that outlasts the interval must not run beside the next one — and nothing more.
        ///
        ///     <para>
        ///     It used to be <c>static</c> on a class with no type parameters, so there was exactly
        ///     one for the whole process, held across each refresh's I/O. Every cache refresh in an
        ///     application was therefore serialised against every other: ten stores on a five-minute
        ///     refresh queued behind each other, and one slow source stalled all of them. See issue
        ///     #35.
        ///     </para>
        /// </summary>
        private readonly SemaphoreSlim semaphore = new(1, 1);

        /// <summary>
        ///     Completed by the first refresh that runs to completion. It is what
        ///     <see cref="WaitAsync"/> waits on, and it replaces a <c>bool</c> that was written from
        ///     a timer callback and read without a lock.
        /// </summary>
        private readonly TaskCompletionSource firstLoad = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly string name;
        private readonly TimeSpan refreshInterval;
        private readonly Func<Task> refreshCallback;
        private readonly ILogger logger;
        private Timer? timer;

        public CacheLoader(string name, TimeSpan refreshInterval, Func<Task> refreshCallback, ILogger logger)
        {
            this.name = name;
            this.refreshInterval = refreshInterval;
            this.refreshCallback = refreshCallback;
            this.logger = logger;

            timer = new Timer(RefreshAsync, null, TimeSpan.Zero, refreshInterval);
            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug($"CacheLoader<{name}> Initialized with refresh every {refreshInterval} [TimeCount={Timer.ActiveCount}]");
        }

        /// <summary>
        ///     Waits for the first load to finish, for at most one refresh interval.
        /// </summary>
        /// <returns>
        ///     Whether a load has run to completion. <see langword="false"/> means the wait ran out,
        ///     not that the load failed — and it does not mean the load *succeeded* either: a
        ///     callback that catches its own exceptions returns normally, and this cannot tell that
        ///     apart from a real success. Callers that need to know whether there is data must track
        ///     it themselves; the caches in cl2j.DataStore do.
        /// </returns>
        public async Task<bool> WaitAsync()
        {
            //The overwhelmingly common case once the application is warm: no allocation, no timer,
            //no await.
            if (firstLoad.Task.IsCompleted)
                return true;

            try
            {
                //Waits on the load itself. This used to spin on Thread.Sleep(100) inside an async
                //method, which held a thread pool thread for the whole wait — up to five minutes,
                //at the refresh intervals real applications use — and added up to a tenth of a
                //second of latency after the load had already finished. On a cold start, where
                //several caches are loading and requests are arriving, those held threads are what
                //the loads themselves needed. See issue #35.
                await firstLoad.Task.WaitAsync(refreshInterval);
                return true;
            }
            catch (TimeoutException)
            {
                return false;
            }
        }

        private async void RefreshAsync(object? state)
        {
            //async void: anything that escapes here takes the process down rather than failing a
            //call, so nothing may escape. The wait and the release are inside the guard too — both
            //throw once the semaphore is disposed, which happens to any refresh still queued when
            //the loader is disposed.
            try
            {
                await semaphore.WaitAsync();
                try
                {
                    await refreshCallback();
                    firstLoad.TrySetResult();
                }
                finally
                {
                    semaphore.Release();
                }
            }
            catch (ObjectDisposedException)
            {
                //Disposed while this refresh was queued behind another. There is nothing to load
                //into and nobody left to tell.
            }
            catch (Exception ex)
            {
                if (logger.IsEnabled(LogLevel.Error))
                    logger.LogError(ex, $"CacheLoader<{name}> : Unexpected error while doing the refresh.");
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
                semaphore.Dispose();
            }
        }
    }
}
