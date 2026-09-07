using System.Diagnostics;
using cl2j.Tooling;
using Microsoft.Extensions.Logging;

namespace cl2j.DataStore.List
{
    public class DataStoreListLoadCache<TValue> : IDataStoreListLoad<TValue>, Tooling.Observers.IObservable<IReadOnlyList<TValue>>, IDataStoreWarmable, IDisposable
    {
        private readonly CacheLoader cacheLoader;
        private List<TValue> cache = [];

        private static readonly SemaphoreSlim semaphore = new(1, 1);

        private readonly Tooling.Observers.Observable<IReadOnlyList<TValue>> observable = new();

        //Kept so GetAllAsync can say which store, and why, rather than answering empty.
        private readonly string name;
        //Set only where a load actually succeeded. CacheLoader.WaitAsync cannot answer this:
        //the callback below catches its own failures, so it returns normally and the loader
        //marks itself loaded either way.
        private volatile bool loadedSuccessfully;
        private Exception? lastLoadFailure;

        public DataStoreListLoadCache(string name, IDataStoreListLoad<TValue> dataStore, TimeSpan refreshInterval, ILogger logger)
        {
            this.name = name;
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

                    lastLoadFailure = null;
                    loadedSuccessfully = true;

                    if (logger.IsEnabled(LogLevel.Debug))
                        logger.LogDebug($"DataStoreListLoadCache<{name}> --> {cache.Count} {name}(s) in {sw.ElapsedMilliseconds}ms");
                }
                catch (Exception ex)
                {
                    lastLoadFailure = ex;

                    if (logger.IsEnabled(LogLevel.Critical))
                        logger.LogCritical(ex, $"DataStoreListLoadCache<{name}> --> Unable to read the entities");
                }
            }, logger);
        }

        public string Name => name;

        public Task WarmAsync() => GetAllAsync();

        public async Task<IReadOnlyList<TValue>> GetAllAsync()
        {
            await WaitForFirstLoadAsync();
            return cache.AsReadOnly();
        }


        /// <summary>
        ///     Blocks until the cache has loaded once, and refuses to answer if it never did.
        ///
        ///     <para>
        ///     A refresh that fails is caught, logged Critical and left there: a cache that has
        ///     data should go on serving it, and a transient outage must not take the process
        ///     down. But before the *first* successful load there is nothing to serve, and
        ///     answering "nothing" is a lie the caller cannot see through — an unreadable source
        ///     and an empty one were indistinguishable, and every caller read them as empty. See
        ///     issue #32.
        ///     </para>
        /// </summary>
        private async Task WaitForFirstLoadAsync()
        {
            await cacheLoader.WaitAsync();
            if (loadedSuccessfully)
                return;

            throw new InvalidOperationException(
                $"The data store '{name}' has not loaded, so there is nothing to answer with. This is not the same as it being empty.",
                lastLoadFailure);
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