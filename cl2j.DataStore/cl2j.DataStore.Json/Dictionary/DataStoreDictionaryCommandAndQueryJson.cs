using cl2j.DataStore.Dictionary;
using cl2j.FileStorage.Core;
using cl2j.FileStorage.Extensions;
using Microsoft.Extensions.Logging;

namespace cl2j.DataStore.Json.Dictionary
{
    /// <summary>
    ///     A dictionary store backed by one JSON file.
    ///
    ///     <para>
    ///     Every write is a read of the whole file, a change, and a write of the whole file. That
    ///     is what a JSON-backed store is: there is nothing smaller to update. The semaphore covers
    ///     all three, because two writes that each read the same file would have the second
    ///     overwrite the first.
    ///     </para>
    ///
    ///     <para>
    ///     The semaphore is static, so it is shared by every instance closed over the same key and
    ///     value types. That over-serialises two stores pointing at different files, and it is the
    ///     safe direction: it never under-serialises two pointing at the same one. It protects
    ///     nothing across processes.
    ///     </para>
    /// </summary>
    public class DataStoreDictionaryCommandAndQueryJson<TKey, TValue>(IFileStorageProvider fileStorageProvider, string filename, ILogger logger, bool indent = false) : IDataStoreDictionaryCommandAndQuery<TKey, TValue> where TKey : notnull
    {
        private static readonly SemaphoreSlim semaphore = new(1, 1);

        public async Task<IReadOnlyDictionary<TKey, TValue>> GetAllAsync()
        {
            return await fileStorageProvider.GetDictionaryValuesAsync<TKey, TValue>(filename, logger);
        }

        public async Task<TValue?> GetByIdAsync(TKey key)
        {
            var items = await GetAllAsync();

            return items.TryGetValue(key, out var value) ? value : default;
        }

        /// <summary>
        ///     Adds an entry whose key is not there yet. A key already in the store is refused, as
        ///     the interface says and as <c>Dictionary.Add</c> does — the caller asked to insert,
        ///     not to replace, and quietly doing the other one loses the value that was there.
        /// </summary>
        public async Task InsertAsync(TKey key, TValue entity)
        {
            await MutateAsync(items =>
            {
                if (!items.TryAdd(key, entity))
                    throw new ArgumentException($"An entry with the key '{key}' is already in '{filename}'.", nameof(key));
            });
        }

        /// <summary>
        ///     Replaces the value of an entry that is there. A key that is absent is refused rather
        ///     than inserted: <see cref="InsertAsync"/> requires the key to be absent, so this
        ///     requiring it to be present is the symmetric half, and a typo that silently created a
        ///     new entry would look exactly like a successful update.
        /// </summary>
        public async Task UpdateAsync(TKey key, TValue entity)
        {
            await MutateAsync(items =>
            {
                if (!items.ContainsKey(key))
                    throw new KeyNotFoundException($"No entry with the key '{key}' in '{filename}'.");

                items[key] = entity;
            });
        }

        /// <summary>
        ///     Removes an entry. A key that is not there is not an error — the store ends up in the
        ///     state the caller asked for either way, and the cache decorator that wraps this store
        ///     tolerates absence too.
        /// </summary>
        public async Task DeleteAsync(TKey key)
        {
            await MutateAsync(items => items.Remove(key));
        }

        public async Task ReplaceAllByAsync(Dictionary<TKey, TValue> items)
        {
            await semaphore.WaitAsync();
            try
            {
                await WriteAsync(items);
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <summary>
        ///     Read, change, write — under the lock, so the three cannot interleave with another
        ///     write. A change that throws leaves the file untouched, since the write only happens
        ///     after it returns.
        /// </summary>
        private async Task MutateAsync(Action<Dictionary<TKey, TValue>> mutate)
        {
            await semaphore.WaitAsync();
            try
            {
                var items = await fileStorageProvider.GetDictionaryValuesAsync<TKey, TValue>(filename, logger);

                mutate(items);

                await WriteAsync(items);
            }
            finally
            {
                semaphore.Release();
            }
        }

        private async Task WriteAsync(IDictionary<TKey, TValue> items)
        {
            await fileStorageProvider.WriteJsonObjectAsync(filename, items, indent, null);
        }
    }
}
