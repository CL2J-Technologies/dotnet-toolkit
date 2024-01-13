using System.Diagnostics;
using cl2j.FileStorage.Core;
using cl2j.FileStorage.Extensions;
using cl2j.Tooling.Exceptions;
using Microsoft.Extensions.Logging;

namespace cl2j.DataStore.Json
{
    public class DataStoreJson<TKey, TValue> : DataStoreBase<TKey, TValue>
    {
        private readonly IFileStorageProvider fileStorageProvider;
        private readonly string filename;
        private readonly ILogger logger;
        private readonly bool indent;

        private static readonly SemaphoreSlim semaphore = new(1, 1);

        public DataStoreJson(IFileStorageProvider fileStorageProvider, string filename, Func<TValue, TKey> getKeyPredicate, ILogger logger, bool indent = false)
            : base(getKeyPredicate)
        {
            this.fileStorageProvider = fileStorageProvider;
            this.filename = filename;
            this.logger = logger;
            this.indent = indent;
        }

        public override async Task<List<TValue>> GetAllAsync()
        {
            var sw = Stopwatch.StartNew();
            var list = await fileStorageProvider.ReadJsonObjectAsync<List<TValue>>(filename, null);
            logger.LogTrace($"DataStoreJson.GetAllAsync<{typeof(TValue).Name}>() -> {list.Count} in {sw.ElapsedMilliseconds}ms");
            return list;
        }

        public override async Task<TValue?> GetByIdAsync(TKey key)
        {
            var list = await GetAllAsync();
            return FirstOrDefault(list, key);
        }

        public override async Task InsertAsync(TValue entity)
        {
            await semaphore.WaitAsync();
            try
            {
                var list = await GetAllAsync();

                int index = FindIndex(list, entity);
                if (index >= 0)
                    throw new ConflictException();

                list.Add(entity);

                await WriteAsync(list);
            }
            catch (Exception ex)
            {
                logger.LogTrace(ex, "Unexpected error during Insert");
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
                var list = await GetAllAsync();

                int index = FindIndex(list, entity);
                if (index < 0)
                    throw new NotFoundException();

                list[index] = entity;

                await WriteAsync(list);
            }
            catch (Exception ex)
            {
                logger.LogTrace(ex, "Unexpected error during Updte");
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
                var list = await GetAllAsync();

                var nb = RemoveAll(list, key);
                if (nb <= 0)
                    throw new NotFoundException();

                await WriteAsync(list);
            }
            catch (Exception ex)
            {
                logger.LogTrace(ex, "Unexpected error during Delete");
            }
            finally
            {
                semaphore.Release();
            }
        }

        public override async Task ReplaceAllByAsync(IDictionary<TKey, TValue> items)
        {
            await semaphore.WaitAsync();
            try
            {
                await WriteAsync(items.Values.ToList());
            }
            finally
            {
                semaphore.Release();
            }
        }

        private async Task WriteAsync(IEnumerable<TValue> list)
        {
            await fileStorageProvider.WriteJsonObjectAsync(filename, list, indent, null);
        }
    }
}