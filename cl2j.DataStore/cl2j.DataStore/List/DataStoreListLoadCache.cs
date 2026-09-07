using System.Diagnostics;
using cl2j.Tooling;
using Microsoft.Extensions.Logging;

namespace cl2j.DataStore.List
{
    public class DataStoreListLoadCache<TValue> : IDataStoreListLoad<TValue>, Tooling.Observers.IObservable<IReadOnlyList<TValue>>, IDisposable
    {
        private readonly CacheLoader cacheLoader;
        private List<TValue> cache = [];

        private static readonly SemaphoreSlim semaphore = new(1, 1);

        private readonly Tooling.Observers.Observable<IReadOnlyList<TValue>> observable = new();

        public DataStoreListLoadCache(string name, IDataStoreListLoad<TValue> dataStore, TimeSpan refreshInterval, ILogger logger)
        {
            cacheLoader = new CacheLoader(name, refreshInterval, async () =>
            {
                try
                {
                    var sw = Stopwatch.StartNew();
                    //Materialised into a list this cache owns: what the store hands back is
                    //read-only, and the cache has to be able to change its own copy.
                    var tmpCache = new List<TValue>(await dataStore.GetAllAsync());

                    await semaphore.WaitAsync();
                    try
                    {
                        cache = tmpCache;
                        await NotifyAsync(cache.AsReadOnly());
                    }
                    finally
                    {
                        semaphore.Release();
                    }

                    if (logger.IsEnabled(LogLevel.Debug))
                        logger.LogDebug($"DataStoreListLoadCache<{name}> --> {cache.Count} {name}(s) in {sw.ElapsedMilliseconds}ms");
                }
                catch (Exception ex)
                {
                    if (logger.IsEnabled(LogLevel.Critical))
                        logger.LogCritical(ex, $"DataStoreListLoadCache<{name}> --> Unable to read the entities");
                }
            }, logger);
        }

        public async Task<IReadOnlyList<TValue>> GetAllAsync()
        {
            await cacheLoader.WaitAsync();
            return cache.AsReadOnly();
        }

        public bool Subscribe(Tooling.Observers.IObserver<IReadOnlyList<TValue>> observer)
        {
            return observable.Subscribe(observer);
        }

        public async Task NotifyAsync(IReadOnlyList<TValue> t)
        {
            await observable.NotifyAsync(t);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
                cacheLoader.Dispose();
        }
    }
}