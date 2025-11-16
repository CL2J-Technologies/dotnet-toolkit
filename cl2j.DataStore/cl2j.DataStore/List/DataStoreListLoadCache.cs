using System.Diagnostics;
using cl2j.Tooling;
using Microsoft.Extensions.Logging;

namespace cl2j.DataStore.List
{
    public class DataStoreListLoadCache<TValue> : IDataStoreListLoad<TValue>, Tooling.Observers.IObservable<List<TValue>>, IDisposable
    {
        private readonly CacheLoader cacheLoader;
        private List<TValue> cache = [];

        private static readonly SemaphoreSlim semaphore = new(1, 1);

        private readonly Tooling.Observers.Observable<List<TValue>> observable = new();

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
                        await NotifyAsync(cache);
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

        public bool Subscribe(Tooling.Observers.IObserver<List<TValue>> observer)
        {
            return observable.Subscribe(observer);
        }

        public async Task NotifyAsync(List<TValue> t)
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