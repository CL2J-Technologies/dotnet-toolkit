using Microsoft.Extensions.Logging;

namespace cl2j.DataStore.Dictionary
{
    public class DataStoreDictionaryCommandAndQueryCache<TKey, TValue> : DataStoreDictionaryLoadCache<TKey, TValue>, IDataStoreDictionaryCommandAndQuery<TKey, TValue> where TKey : notnull
    {
        private readonly IDataStoreDictionaryCommandAndQuery<TKey, TValue> dataStore;

        public DataStoreDictionaryCommandAndQueryCache(string name, IDataStoreDictionaryCommandAndQuery<TKey, TValue> dataStore, TimeSpan refreshInterval, ILogger logger)
            : base(name, dataStore, refreshInterval, logger)
        {
            this.dataStore = dataStore;
        }

        public async Task<TValue?> GetByIdAsync(TKey key)
        {
            await cacheLoader.WaitAsync();

            cache.TryGetValue(key, out var value);
            return value;
        }

        public async Task InsertAsync(TKey key, TValue entity)
        {
            await semaphore.WaitAsync();
            try
            {
                await dataStore.InsertAsync(key, entity);
                cache.Add(key, entity);
                await NotifyAsync(cache);
            }
            finally
            {
                semaphore.Release();
            }
        }

        public async Task UpdateAsync(TKey key, TValue entity)
        {
            await semaphore.WaitAsync();
            try
            {
                await dataStore.UpdateAsync(key, entity);
                if (cache.ContainsKey(key))
                {
                    cache[key] = entity;
                    await NotifyAsync(cache);
                }
            }
            finally
            {
                semaphore.Release();
            }
        }

        public async Task DeleteAsync(TKey key)
        {
            await semaphore.WaitAsync();
            try
            {
                await dataStore.DeleteAsync(key);

                if (cache.Remove(key))
                    await NotifyAsync(cache);
            }
            finally
            {
                semaphore.Release();
            }
        }

        public async Task ReplaceAllByAsync(Dictionary<TKey, TValue> items)
        {
            await semaphore.WaitAsync();
            try
            {
                await dataStore.ReplaceAllByAsync(items);
                cache = items;
                await NotifyAsync(cache);
            }
            finally
            {
                semaphore.Release();
            }
        }
    }
}