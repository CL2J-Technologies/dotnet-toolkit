using System.Diagnostics;
using cl2j.Tooling;
using Microsoft.Extensions.Logging;

namespace cl2j.DataStore.List
{
    public class DataStoreListCommandAndQueryCache<TKey, TValue> : DataStoreListCommandAndQueryBase<TKey, TValue>, Tooling.Observers.IObservable<List<TValue>>, IDisposable
    {
        private readonly CacheLoader cacheLoader;
        private readonly IDataStoreListCommandAndQuery<TKey, TValue> dataStore;
        private List<TValue> cache = [];

        private readonly Tooling.Observers.Observable<List<TValue>> observable = new();

        private static readonly SemaphoreSlim semaphore = new(1, 1);

        public DataStoreListCommandAndQueryCache(string name, IDataStoreListCommandAndQuery<TKey, TValue> dataStore, TimeSpan refreshInterval, Func<TValue, TKey> getKeyPredicate, ILogger logger, Func<TValue, object?>? orderbyPredicate = null, bool? ascending = null)
            : base(getKeyPredicate)
        {
            this.dataStore = dataStore;

            cacheLoader = new CacheLoader(name, refreshInterval, async () =>
            {
                try
                {
                    var sw = Stopwatch.StartNew();
                    var tmpCache = await dataStore.GetAllAsync();

                    if (orderbyPredicate != null)
                    {
                        if (ascending == null || ascending.Value)
                            tmpCache = [.. tmpCache.OrderBy(orderbyPredicate)];
                        else
                            tmpCache = [.. tmpCache.OrderByDescending(orderbyPredicate)];
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

                    logger.LogDebug($"DataStoreCache<{name}> --> {cache.Count} {name}(s) in {sw.ElapsedMilliseconds}ms");
                }
                catch (Exception ex)
                {
                    logger.LogCritical(ex, $"DataStoreCache<{name}> --> Unable to read the entities");
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

        public override async Task InsertAsync(TValue entity)
        {
            await semaphore.WaitAsync();
            try
            {
                await dataStore.InsertAsync(entity);
                cache.Add(entity);
                await NotifyAsync(cache);
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

                var index = FindIndex(cache, entity);
                if (index >= 0)
                {
                    cache[index] = entity;
                    await NotifyAsync(cache);
                }
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
                    await NotifyAsync(cache);
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
                cache = [.. items];
                await NotifyAsync(cache);
            }
            finally
            {
                semaphore.Release();
            }
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