using System.Diagnostics;
using cl2j.Tooling;
using Microsoft.Extensions.Logging;

namespace cl2j.DataStore.Dictionary
{
    public abstract class DataStoreDictionaryLoadCache<TKey, TValue> : Tooling.Observers.IObservable<Dictionary<TKey, TValue>> where TKey : notnull
    {
        protected readonly CacheLoader cacheLoader;
        protected Dictionary<TKey, TValue> cache = new();
        protected static readonly SemaphoreSlim semaphore = new(1, 1);

        private readonly Tooling.Observers.Observable<Dictionary<TKey, TValue>> observable = new();

        public DataStoreDictionaryLoadCache(string name, IDataStoreDictionaryLoad<TKey, TValue> dataStore, TimeSpan refreshInterval, ILogger logger)
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
                        await NotifyAsync(cache);
                    }
                    finally
                    {
                        semaphore.Release();
                    }

                    logger.LogDebug($"DataStoreDictionary.LoadCache<{name}> --> {cache.Count} {name}(s) in {sw.ElapsedMilliseconds}ms");
                }
                catch (Exception ex)
                {
                    logger.LogCritical(ex, $"DataStoreDictionary.LoadCache<{name}> --> Unable to read the entities");
                }
            }, logger);
        }

        public async Task<Dictionary<TKey, TValue>> GetAllAsync()
        {
            await cacheLoader.WaitAsync();
            return cache;
        }

        public bool Subscribe(Tooling.Observers.IObserver<Dictionary<TKey, TValue>> observer)
        {
            return observable.Subscribe(observer);
        }

        public async Task NotifyAsync(Dictionary<TKey, TValue> t)
        {
            await observable.NotifyAsync(t);
        }
    }
}