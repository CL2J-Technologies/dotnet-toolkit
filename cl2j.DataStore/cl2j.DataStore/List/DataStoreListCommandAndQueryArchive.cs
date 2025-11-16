using cl2j.FileStorage.Core;
using cl2j.FileStorage.Extensions;

namespace cl2j.DataStore.List
{
    public class DataStoreListCommandAndQueryArchive<TKey, TValue>(IDataStoreListCommandAndQuery<TKey, TValue> dataStore, IFileStorageProvider fileStorageProvider, string filename) : IDataStoreListCommandAndQuery<TKey, TValue>
    {
        public async Task<List<TValue>> GetAllAsync()
        {
            return await dataStore.GetAllAsync();
        }

        public async Task<TValue?> GetByIdAsync(TKey key)
        {
            return await dataStore.GetByIdAsync(key);
        }

        public async Task InsertAsync(TValue entity)
        {
            await dataStore.InsertAsync(entity);
            await WriteArchiveAsync();
        }

        public async Task UpdateAsync(TValue entity)
        {
            await dataStore.UpdateAsync(entity);
            await WriteArchiveAsync();
        }

        public async Task DeleteAsync(TKey key)
        {
            await dataStore.DeleteAsync(key);
            await WriteArchiveAsync();
        }

        public async Task ReplaceAllByAsync(ICollection<TValue> items)
        {
            await dataStore.ReplaceAllByAsync(items);
            await WriteArchiveAsync();
        }

        private async Task WriteArchiveAsync()
        {
            try
            {
                var entities = await dataStore.GetAllAsync();

                var fn = string.Format(filename, DateTimeOffset.UtcNow.ToString("yyyyMMdd-hhmm"));
                await fileStorageProvider.WriteJsonObjectAsync(fn, entities);
            }
            finally
            {
            }
        }
    }
}
