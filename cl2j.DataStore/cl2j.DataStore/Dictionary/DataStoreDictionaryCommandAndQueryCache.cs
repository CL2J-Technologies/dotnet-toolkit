using Microsoft.Extensions.Logging;

namespace cl2j.DataStore.Dictionary
{
    public class DataStoreDictionaryCommandAndQueryCache<TKey, TValue>(string name, IDataStoreDictionaryCommandAndQuery<TKey, TValue> dataStore, TimeSpan refreshInterval, ILogger logger) : DataStoreDictionaryLoadCache<TKey, TValue>(name, dataStore, refreshInterval, logger), IDataStoreDictionaryCommandAndQuery<TKey, TValue> where TKey : notnull
    {
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
                await NotifyAsync(AsReadOnly(cache));
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <summary>
        ///     Writes through, then makes the cache agree — whether or not it already held the key.
        ///
        ///     <para>
        ///     It used to update the cache only when the key was already in it. A row that reached
        ///     the store after the last refresh — put there by another process, or by another
        ///     instance of this application — was therefore written and then stayed invisible here
        ///     until the next one, with no exception and nothing said to observers. See issue #32.
        ///     </para>
        /// </summary>
        public async Task UpdateAsync(TKey key, TValue entity)
        {
            await semaphore.WaitAsync();
            try
            {
                await dataStore.UpdateAsync(key, entity);
                cache[key] = entity;
                await NotifyAsync(AsReadOnly(cache));
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
                    await NotifyAsync(AsReadOnly(cache));
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <summary>
        ///     Replaces both the store and the cache, taking a copy of what it is given.
        ///
        ///     <para>
        ///     It used to keep the caller's own dictionary as the cache, so whoever passed it could
        ///     go on changing what the cache held — outside the semaphore, with no observer told.
        ///     The list cache already copied; the two now behave the same way. See issue #32.
        ///     </para>
        /// </summary>
        public async Task ReplaceAllByAsync(Dictionary<TKey, TValue> items)
        {
            await semaphore.WaitAsync();
            try
            {
                await dataStore.ReplaceAllByAsync(items);
                cache = new Dictionary<TKey, TValue>(items);
                await NotifyAsync(AsReadOnly(cache));
            }
            finally
            {
                semaphore.Release();
            }
        }
    }
}