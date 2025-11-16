using cl2j.DataStore.List;
using cl2j.FileStorage.Core;
using cl2j.FileStorage.Extensions;
using cl2j.Tooling.Exceptions;
using Microsoft.Extensions.Logging;

namespace cl2j.DataStore.Json.List
{
    public class DataStoreListCommandAndQueryJson<TKey, TValue>(IFileStorageProvider fileStorageProvider, string filename, Func<TValue, TKey> getKeyPredicate, ILogger logger, bool indent = false) : DataStoreListCommandAndQueryBase<TKey, TValue>(getKeyPredicate)
    {
        private static readonly SemaphoreSlim semaphore = new(1, 1);

        public override async Task<List<TValue>> GetAllAsync()
        {
            return await fileStorageProvider.GetListValuesAsync<TValue>(filename, logger);
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

        public override async Task ReplaceAllByAsync(ICollection<TValue> items)
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

        private async Task WriteAsync(IEnumerable<TValue> list)
        {
            await fileStorageProvider.WriteJsonObjectAsync(filename, list, indent, null);
        }

        private int RemoveAll(List<TValue> list, TKey key)
        {
            return list.RemoveAll(item => EqualityComparer<TKey>.Default.Equals(getKeyPredicate(item), key));
        }
    }
}