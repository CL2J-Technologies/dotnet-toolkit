using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace cl2j.Tooling
{
    public abstract class CacheableObject<T>
    {
        private readonly string name;
        private readonly ILogger logger;

        private readonly CacheLoader cacheLoader;
        private List<T> cache = new();
        protected static readonly SemaphoreSlim semaphore = new(1, 1);

        public CacheableObject(string name, TimeSpan refreshInterval, ILogger logger)
        {
            cacheLoader = new CacheLoader(name, refreshInterval, RefreshCache, logger);
            this.name = name;
            this.logger = logger;
        }

        protected abstract Task<List<T>> LoadCache();

        protected async Task<List<T>> GetCache()
        {
            await cacheLoader.WaitAsync();
            return cache;
        }

        protected async Task WaitForCache()
        {
            await cacheLoader.WaitAsync();
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
                    logger.LogError($"{name}: Unexzpect error", ex);
                }
                finally
                {
                    semaphore.Release();
                }
                logger.LogDebug($"{name} --> {cache.Count} in {sw.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, $"{name} --> Unable to load cache");
            }
        }

        protected async Task<List<T>> AddCacheObject(T o)
        {
            await cacheLoader.WaitAsync();

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
            await cacheLoader.WaitAsync();

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
    }
}
