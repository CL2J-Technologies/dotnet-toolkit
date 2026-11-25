using System.Globalization;
using cl2j.FileStorage.Core;
using cl2j.FileStorage.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace cl2j.DataStore.List
{
    /// <summary>
    ///     Writes a snapshot of the whole store to file storage after every change.
    ///
    ///     <para>
    ///     A snapshot that cannot be written does not fail the change that was already applied: it
    ///     is logged at Error instead. Every snapshot reads the whole store back, by design: the
    ///     archive decorates any store and knows neither its key nor what changed behind its back, so
    ///     a copy kept here could drift from the store.
    ///     </para>
    /// </summary>
    /// <param name="dataStore">The store whose changes are archived.</param>
    /// <param name="fileStorageProvider">Where the snapshots are written.</param>
    /// <param name="filename">
    ///     A composite format string; <c>{0}</c> receives the timestamp, to the minute.
    /// </param>
    /// <param name="now">
    ///     Where the timestamp comes from. It exists so a test can pin it — which is how the
    ///     twelve-hour clock in the format string went unnoticed: with no way to choose the hour,
    ///     nothing ever compared a morning snapshot with an afternoon one.
    /// </param>
    /// <param name="logger">
    ///     Where a failed archive write is reported, at <see cref="LogLevel.Error"/>. Optional; a
    ///     failure is not reported anywhere when there is none, but it never reaches the caller.
    /// </param>
    /// <param name="jsonOptions">
    ///     The types a <c>$type</c> may name, for entities that have polymorphic members. None by
    ///     default, in which case a snapshot of such an entity is refused: it is logged at Error
    ///     like any failed snapshot, and the change itself is not affected.
    /// </param>
    public class DataStoreListCommandAndQueryArchive<TKey, TValue>(
        IDataStoreListCommandAndQuery<TKey, TValue> dataStore,
        IFileStorageProvider fileStorageProvider,
        string filename,
        Func<DateTimeOffset>? now = null,
        ILogger? logger = null,
        JsonStorageOptions? jsonOptions = null) : IDataStoreListCommandAndQuery<TKey, TValue>
    {
        private readonly Func<DateTimeOffset> now = now ?? (() => DateTimeOffset.UtcNow);
        private readonly ILogger logger = logger ?? NullLogger.Instance;

        public async Task<IReadOnlyList<TValue>> GetAllAsync()
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
            //The store has already accepted the change by now. A failure here is reported to the log,
            //not to the caller: throwing would tell the caller that an operation which succeeded had
            //failed, and a retry would apply it twice. The price is that an archive can stop being
            //written without the caller noticing, which is why it is logged at Error.
            string? fn = null;
            try
            {
                var entities = await dataStore.GetAllAsync();

                //HH, not hh. It used to be the twelve-hour clock, so 13:45 and 01:45 produced the same
                //name and the afternoon snapshot overwrote the morning one — half a day of archives
                //gone, with nothing to show for it.
                fn = string.Format(CultureInfo.InvariantCulture, filename, now().ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture));
                await fileStorageProvider.WriteJsonObjectAsync(fn, entities, jsonOptions);
            }
            catch (Exception ex)
            {
                if (logger.IsEnabled(LogLevel.Error))
                    logger.LogError(ex, "DataStore archive '{Archive}' could not be written; the change itself was applied", fn ?? filename);
            }
        }
    }
}
