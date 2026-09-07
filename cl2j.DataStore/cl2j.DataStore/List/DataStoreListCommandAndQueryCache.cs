using System.Diagnostics;
using cl2j.Tooling;
using Microsoft.Extensions.Logging;

namespace cl2j.DataStore.List
{
    public class DataStoreListCommandAndQueryCache<TKey, TValue> : DataStoreListCommandAndQueryBase<TKey, TValue>, Tooling.Observers.IObservable<IReadOnlyList<TValue>>, IDataStoreWarmable, IDisposable
    {
        private readonly CacheLoader cacheLoader;
        private readonly IDataStoreListCommandAndQuery<TKey, TValue> dataStore;
        private List<TValue> cache = [];

        private readonly Tooling.Observers.Observable<IReadOnlyList<TValue>> observable = new();

        private static readonly SemaphoreSlim semaphore = new(1, 1);

        //Kept so GetAllAsync can say which store, and why, rather than answering empty.
        private readonly string name;
        //Set only where a load actually succeeded. CacheLoader.WaitAsync cannot answer this:
        //the callback below catches its own failures, so it returns normally and the loader
        //marks itself loaded either way.
        private volatile bool loadedSuccessfully;
        private Exception? lastLoadFailure;

        //Kept, so the order asked for can be restored after a write and not only at load.
        private readonly Func<TValue, object?>? orderbyPredicate;
        private readonly bool ascending;

        public DataStoreListCommandAndQueryCache(string name, IDataStoreListCommandAndQuery<TKey, TValue> dataStore, TimeSpan refreshInterval, Func<TValue, TKey> getKeyPredicate, ILogger logger, Func<TValue, object?>? orderbyPredicate = null, bool? ascending = null)
            : base(getKeyPredicate)
        {
            this.dataStore = dataStore;
            this.name = name;
            this.orderbyPredicate = orderbyPredicate;
            this.ascending = ascending ?? true;

            cacheLoader = new CacheLoader(name, refreshInterval, async () =>
            {
                try
                {
                    var sw = Stopwatch.StartNew();
                    var tmpCache = Sorted(new List<TValue>(await dataStore.GetAllAsync()));

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
                        logger.LogDebug($"DataStoreCache<{name}> --> {cache.Count} {name}(s) in {sw.ElapsedMilliseconds}ms");
                }
                catch (Exception ex)
                {
                    lastLoadFailure = ex;

                    if (logger.IsEnabled(LogLevel.Critical))
                        logger.LogCritical(ex, $"DataStoreCache<{name}> --> Unable to read the entities");
                }
            }, logger);
        }

        public string Name => name;

        public Task WarmAsync() => GetAllAsync();

        public override async Task<IReadOnlyList<TValue>> GetAllAsync()
        {
            await WaitForFirstLoadAsync();
            return cache.AsReadOnly();
        }

        public override async Task<TValue?> GetByIdAsync(TKey key)
        {
            await WaitForFirstLoadAsync();
            return FirstOrDefault(cache, key);
        }

        public override async Task InsertAsync(TValue entity)
        {
            await semaphore.WaitAsync();
            try
            {
                await dataStore.InsertAsync(entity);
                Upsert(entity);
                await NotifyAsync(cache.AsReadOnly());
            }
            finally
            {
                semaphore.Release();
            }
        }

        public override async Task UpdateAsync(TValue entity)
        {
            await semaphore.WaitAsync();
            try
            {
                await dataStore.UpdateAsync(entity);
                Upsert(entity);
                await NotifyAsync(cache.AsReadOnly());
            }
            finally
            {
                semaphore.Release();
            }
        }

        public override async Task DeleteAsync(TKey key)
        {
            await semaphore.WaitAsync();
            try
            {
                await dataStore.DeleteAsync(key);

                var index = FindIndex(cache, key);
                if (index >= 0)
                {
                    cache.RemoveAt(index);
                    await NotifyAsync(cache.AsReadOnly());
                }
            }
            finally
            {
                semaphore.Release();
            }
        }

        public override async Task ReplaceAllByAsync(ICollection<TValue> items)
        {
            await semaphore.WaitAsync();
            try
            {
                await dataStore.ReplaceAllByAsync(items);
                cache = Sorted([.. items]);
                await NotifyAsync(cache.AsReadOnly());
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <summary>
        ///     Puts the item where the cache says it belongs: replacing the one carrying the same
        ///     key if there is one, appending otherwise, and then restoring the order that was
        ///     asked for.
        ///
        ///     <para>
        ///     The ordering predicate used to be applied when the cache loaded and never again, so
        ///     anything written afterwards sat at the end until the next refresh — an order that
        ///     was neither the one asked for nor the store's. Appending rather than replacing also
        ///     let a second copy of one key into the cache when the store accepted the insert. See
        ///     issue #32.
        ///     </para>
        ///
        ///     <para>
        ///     The sort is a full one per write. Writes here always cost a round trip to the store
        ///     first, so the sort is not what makes them slow.
        ///     </para>
        /// </summary>
        private void Upsert(TValue entity)
        {
            var index = FindIndex(cache, entity);
            if (index >= 0)
                cache[index] = entity;
            else
                cache.Add(entity);

            cache = Sorted(cache);
        }

        private List<TValue> Sorted(List<TValue> items)
        {
            if (orderbyPredicate is null)
                return items;

            return ascending
                ? [.. items.OrderBy(orderbyPredicate)]
                : [.. items.OrderByDescending(orderbyPredicate)];
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