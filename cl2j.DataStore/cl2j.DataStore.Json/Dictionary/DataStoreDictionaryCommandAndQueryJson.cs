using cl2j.DataStore.Dictionary;
using cl2j.FileStorage.Core;
using cl2j.FileStorage.Extensions;
using Microsoft.Extensions.Logging;

namespace cl2j.DataStore.Json.Dictionary
{
    public class DataStoreDictionaryCommandAndQueryJson<TKey, TValue>(IFileStorageProvider fileStorageProvider, string filename, ILogger logger, bool indent = false) : IDataStoreDictionaryCommandAndQuery<TKey, TValue> where TKey : notnull
    {
        private static readonly SemaphoreSlim semaphore = new(1, 1);

        public async Task<Dictionary<TKey, TValue>> GetAllAsync()
        {
            return await fileStorageProvider.GetDictionaryValuesAsync<TKey, TValue>(filename, logger);
        }

        public async Task<TValue?> GetByIdAsync(TKey key)
        {
            await Task.CompletedTask;
            throw new NotImplementedException();
        }

        public async Task InsertAsync(TKey key, TValue entity)
        {
            await Task.CompletedTask;
            throw new NotImplementedException();
        }

        public async Task UpdateAsync(TKey key, TValue entity)
        {
            await Task.CompletedTask;
            throw new NotImplementedException();
        }

        public async Task DeleteAsync(TKey key)
        {
            await Task.CompletedTask;
            throw new NotImplementedException();
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

        private async Task WriteAsync(IDictionary<TKey, TValue> items)
        {
            await fileStorageProvider.WriteJsonObjectAsync(filename, items, indent, null);
        }
    }
}