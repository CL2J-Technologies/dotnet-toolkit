using System.Diagnostics;
using cl2j.Tooling;
using Microsoft.Extensions.Logging;

namespace cl2j.DataStore.List
{
    public class DataStoreListLoadCache<TValue> : IDataStoreListLoad<TValue>
    {
        private readonly CacheLoader cacheLoader;
        private List<TValue> cache = new();

        private static readonly SemaphoreSlim semaphore = new(1, 1);

        public DataStoreListLoadCache(string name, IDataStoreListLoad<TValue> dataStore, TimeSpan refreshInterval, ILogger logger)
        {
            cacheLoader = new CacheLoader(name, refreshInterval, async () =>
            {
                try
                {
                    var sw = Stopwatch.StartNew();
                    var tmpCache = await dataStore.GetAllAsync();

                    await semaphore.WaitAsync();
                    try
                    {
                        cache = tmpCache;
                    }
                    finally
                    {
                        semaphore.Release();
                    }

                    logger.LogDebug($"DataStoreListLoadCache<{name}> --> {cache.Count} {name}(s) in {sw.ElapsedMilliseconds}ms");
                }
                catch (Exception ex)
                {
                    logger.LogCritical(ex, $"DataStoreListLoadCache<{name}> --> Unable to read the entities");
                }
            }, logger);
        }

        public async Task<List<TValue>> GetAllAsync()
        {
            await cacheLoader.WaitAsync();
            return cache;
        }
    }
}