using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace cl2j.Tooling
{
    public abstract class CacheableObject<T> : IDisposable
    {
        private readonly string name;
        private readonly ILogger logger;

        /// <summary>
        ///     Created, and so started, on first use rather than in the constructor. The loader's
        ///     timer fires straight away on a thread pool thread, so starting it here let the first
        ///     <see cref="LoadCache"/> run before the derived constructor had assigned its fields:
        ///     that load saw them unset, and the cache stayed empty until the next refresh.
        /// </summary>
        private readonly Lazy<CacheLoader> cacheLoader;
        private List<T> cache = [];
        protected static readonly SemaphoreSlim semaphore = new(1, 1);

        public CacheableObject(string name, TimeSpan refreshInterval, ILogger logger)
        {
            this.name = name;
            this.logger = logger;
            cacheLoader = new Lazy<CacheLoader>(() => new CacheLoader(name, refreshInterval, RefreshCache, logger), LazyThreadSafetyMode.ExecutionAndPublication);
        }

        protected abstract Task<List<T>> LoadCache();

        protected async Task<List<T>> GetCache()
        {
            await cacheLoader.Value.WaitAsync();
            return cache;
        }

        protected async Task WaitForCache()
        {
            await cacheLoader.Value.WaitAsync();
        }

        protected async Task RefreshCache()
        {
            try
            {
                var sw = Stopwatch.StartNew();
                await semaphore.WaitAsync();
                try
                {
                    cache = await LoadCache();
                }
                catch (Exception ex)
                {
                    if (logger.IsEnabled(LogLevel.Error))
                        logger.LogError($"{name}: Unexpected error", ex);
                }
                finally
                {
                    semaphore.Release();
                }
                if (logger.IsEnabled(LogLevel.Debug))
                    logger.LogDebug($"{name} --> {cache.Count} in {sw.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                if (logger.IsEnabled(LogLevel.Critical))
                    logger.LogCritical(ex, $"{name} --> Unable to load cache");
            }
        }

        protected async Task<List<T>> AddCacheObject(T o)
        {
            await cacheLoader.Value.WaitAsync();

            await semaphore.WaitAsync();
            try
            {
                cache.Add(o);
            }
            finally
            {
                semaphore.Release();
            }

            return cache;
        }

        protected async Task<List<T>> UpdateCacheObject(T oldObject, T newObject)
        {
            await cacheLoader.Value.WaitAsync();

            await semaphore.WaitAsync();
            try
            {
                cache.Remove(oldObject);
                cache.Add(newObject);
            }
            finally
            {
                semaphore.Release();
            }

            return cache;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing && cacheLoader.IsValueCreated)
                cacheLoader.Value.Dispose();
        }
    }
}
