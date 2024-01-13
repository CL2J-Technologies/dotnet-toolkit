using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace cl2j.DataStore.Cache
{
    public class DataStoreQueryCache<TKey, TValue> : DataStoreQueryBase<TKey, TValue>, Tooling.Observers.IObservable<List<TValue>>
    {
        private readonly CacheLoader cacheLoader;
        private List<TValue> cache = new();

        private readonly Tooling.Observers.Observable<List<TValue>> observable = new();

        private static readonly SemaphoreSlim semaphore = new(1, 1);

        public DataStoreQueryCache(string name, IDataStoreQuery<TKey, TValue> dataStore, TimeSpan refreshInterval, Func<TValue, TKey> getKeyPredicate, ILogger logger, Func<TValue, object?>? orderbyPredicate = null, bool? ascending = null)
            : base(getKeyPredicate)
        {
            cacheLoader = new CacheLoader(name, refreshInterval, async () =>
            {
                try
                {
                    var sw = Stopwatch.StartNew();
                    var tmpCache = await dataStore.GetAllAsync();

                    if (orderbyPredicate != null)
                    {
                        if (ascending == null || ascending.Value)
                            tmpCache = tmpCache.OrderBy(orderbyPredicate).ToList();
                        else
                            tmpCache = tmpCache.OrderByDescending(orderbyPredicate).ToList();
                    }

                    await semaphore.WaitAsync();
                    try
                    {
                        cache = tmpCache;
                        await NotifyAsync(cache);
                    }
                    finally
                    {
                        semaphore.Release();
                    }

                    logger.LogDebug($"DataStoreQueryCache<{name}> --> {cache.Count} {name}(s) in {sw.ElapsedMilliseconds}ms");
                }
                catch (Exception ex)
                {
                    logger.LogCritical(ex, $"DataStoreQueryCache<{name}> --> Unable to read the entities");
                }
            }, logger);
        }

        public override async Task<List<TValue>> GetAllAsync()
        {
            await cacheLoader.WaitAsync();
            return cache;
        }

        public override async Task<TValue?> GetByIdAsync(TKey key)
        {
            await cacheLoader.WaitAsync();
            return FirstOrDefault(cache, key);
        }

        public bool Subscribe(Tooling.Observers.IObserver<List<TValue>> observer)
        {
            return observable.Subscribe(observer);
        }

        public async Task NotifyAsync(List<TValue> t)
        {
            await observable.NotifyAsync(t);
        }
    }
}