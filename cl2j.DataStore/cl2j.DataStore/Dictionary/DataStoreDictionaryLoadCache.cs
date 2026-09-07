using System.Collections.ObjectModel;
using System.Diagnostics;
using cl2j.Tooling;
using Microsoft.Extensions.Logging;

namespace cl2j.DataStore.Dictionary
{
    public abstract class DataStoreDictionaryLoadCache<TKey, TValue> : Tooling.Observers.IObservable<IReadOnlyDictionary<TKey, TValue>>, IDisposable where TKey : notnull
    {
        protected readonly CacheLoader cacheLoader;
        protected Dictionary<TKey, TValue> cache = [];
        protected static readonly SemaphoreSlim semaphore = new(1, 1);

        private readonly Tooling.Observers.Observable<IReadOnlyDictionary<TKey, TValue>> observable = new();

        public DataStoreDictionaryLoadCache(string name, IDataStoreDictionaryLoad<TKey, TValue> dataStore, TimeSpan refreshInterval, ILogger logger)
        {
            cacheLoader = new CacheLoader(name, refreshInterval, async () =>
            {
                try
                {
                    var sw = Stopwatch.StartNew();
                    //Materialised into a dictionary this cache owns. What the store hands back is
                    //read-only, and the cache has to be able to change its own copy on a write.
                    var tmpCache = new Dictionary<TKey, TValue>(await dataStore.GetAllAsync());

                    await semaphore.WaitAsync();
                    try
                    {
                        cache = tmpCache;
                        await NotifyAsync(AsReadOnly(cache));
                    }
                    finally
                    {
                        semaphore.Release();
                    }

                    if (logger.IsEnabled(LogLevel.Debug))
                        logger.LogDebug($"DataStoreDictionary.LoadCache<{name}> --> {cache.Count} {name}(s) in {sw.ElapsedMilliseconds}ms");
                }
                catch (Exception ex)
                {
                    if (logger.IsEnabled(LogLevel.Critical))
                        logger.LogCritical(ex, $"DataStoreDictionary.LoadCache<{name}> --> Unable to read the entities");
                }
            }, logger);
        }

        /// <summary>
        ///     A read-only view of the cache, not a copy of it.
        ///
        ///     <para>
        ///     This used to hand back the cache itself, so a caller that changed what it got back
        ///     was changing the cache — outside the semaphore, and without any observer being told.
        ///     A copy would close that, but it would also cost a full copy on every read, and a
        ///     shallow one at that: consumers call this on the request path. Wrapping is O(1) and
        ///     the wrapper refuses mutation rather than silently absorbing it. See issue #32.
        ///     </para>
        /// </summary>
        public async Task<IReadOnlyDictionary<TKey, TValue>> GetAllAsync()
        {
            await cacheLoader.WaitAsync();
            return AsReadOnly(cache);
        }

        protected static IReadOnlyDictionary<TKey, TValue> AsReadOnly(Dictionary<TKey, TValue> items) => new ReadOnlyDictionary<TKey, TValue>(items);

        public bool Subscribe(Tooling.Observers.IObserver<IReadOnlyDictionary<TKey, TValue>> observer)
        {
            return observable.Subscribe(observer);
        }

        public async Task NotifyAsync(IReadOnlyDictionary<TKey, TValue> t)
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