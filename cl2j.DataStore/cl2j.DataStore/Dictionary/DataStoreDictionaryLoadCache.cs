using System.Collections.ObjectModel;
using System.Diagnostics;
using cl2j.Tooling;
using Microsoft.Extensions.Logging;

namespace cl2j.DataStore.Dictionary
{
    public abstract class DataStoreDictionaryLoadCache<TKey, TValue> : Tooling.Observers.IObservable<IReadOnlyDictionary<TKey, TValue>>, IDataStoreWarmable, IDisposable where TKey : notnull
    {
        protected readonly CacheLoader cacheLoader;
        protected Dictionary<TKey, TValue> cache = [];
        protected static readonly SemaphoreSlim semaphore = new(1, 1);

        private readonly Tooling.Observers.Observable<IReadOnlyDictionary<TKey, TValue>> observable = new();

        //Kept so GetAllAsync can say which store, and why, rather than answering empty.
        private readonly string name;
        //Set only where a load actually succeeded. CacheLoader.WaitAsync cannot answer this:
        //the callback below catches its own failures, so it returns normally and the loader
        //marks itself loaded either way.
        private volatile bool loadedSuccessfully;
        private Exception? lastLoadFailure;

        public DataStoreDictionaryLoadCache(string name, IDataStoreDictionaryLoad<TKey, TValue> dataStore, TimeSpan refreshInterval, ILogger logger)
        {
            this.name = name;
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

                    lastLoadFailure = null;
                    loadedSuccessfully = true;

                    if (logger.IsEnabled(LogLevel.Debug))
                        logger.LogDebug($"DataStoreDictionary.LoadCache<{name}> --> {cache.Count} {name}(s) in {sw.ElapsedMilliseconds}ms");
                }
                catch (Exception ex)
                {
                    lastLoadFailure = ex;

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
        public string Name => name;

        public Task WarmAsync() => GetAllAsync();

        public async Task<IReadOnlyDictionary<TKey, TValue>> GetAllAsync()
        {
            await WaitForFirstLoadAsync();
            return AsReadOnly(cache);
        }

        /// <summary>
        ///     Blocks until the cache has loaded once, and refuses to answer if it never did.
        ///
        ///     <para>
        ///     A refresh that fails is caught, logged Critical and left there: a cache that has
        ///     data should go on serving it, and a transient outage must not take the process down.
        ///     But before the *first* successful load there is nothing to serve, and answering
        ///     "nothing" is a lie the caller cannot see through — an unreadable source and an empty
        ///     one were indistinguishable, and every caller read them as empty. See issue #32.
        ///     </para>
        /// </summary>
        protected async Task WaitForFirstLoadAsync()
        {
            await cacheLoader.WaitAsync();
            if (loadedSuccessfully)
                return;

            throw new InvalidOperationException(
                $"The data store '{name}' has not loaded, so there is nothing to answer with. This is not the same as it being empty.",
                lastLoadFailure);
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