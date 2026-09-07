using System.Globalization;
using cl2j.FileStorage.Core;
using cl2j.FileStorage.Extensions;

namespace cl2j.DataStore.List
{
    /// <summary>
    ///     Writes a snapshot of the whole store to file storage after every change.
    /// </summary>
    /// <param name="filename">
    ///     A composite format string; <c>{0}</c> receives the timestamp, to the minute.
    /// </param>
    /// <param name="now">
    ///     Where the timestamp comes from. It exists so a test can pin it — which is how the
    ///     twelve-hour clock in the format string went unnoticed: with no way to choose the hour,
    ///     nothing ever compared a morning snapshot with an afternoon one.
    /// </param>
    public class DataStoreListCommandAndQueryArchive<TKey, TValue>(
        IDataStoreListCommandAndQuery<TKey, TValue> dataStore,
        IFileStorageProvider fileStorageProvider,
        string filename,
        Func<DateTimeOffset>? now = null) : IDataStoreListCommandAndQuery<TKey, TValue>
    {
        private readonly Func<DateTimeOffset> now = now ?? (() => DateTimeOffset.UtcNow);

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
            var entities = await dataStore.GetAllAsync();

            //HH, not hh. It used to be the twelve-hour clock, so 13:45 and 01:45 produced the same
            //name and the afternoon snapshot overwrote the morning one — half a day of archives
            //gone, with nothing to show for it.
            var fn = string.Format(CultureInfo.InvariantCulture, filename, now().ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture));
            await fileStorageProvider.WriteJsonObjectAsync(fn, entities);
        }
    }
}
