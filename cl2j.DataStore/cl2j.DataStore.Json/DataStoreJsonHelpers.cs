using cl2j.FileStorage.Core;
using cl2j.FileStorage.Extensions;

namespace cl2j.DataStore.Json
{
    public static class DataStoreJsonHelpers
    {
        public static async Task<T> GetOrFetch<T>(this IFileStorageProvider storageProvider, string fileName, Func<Task<T>> fetchDelegate) where T : new()
        {
            if (await storageProvider.ExistsAsync(fileName))
                return await storageProvider.ReadJsonObjectAsync<T>(fileName);

            var o = await fetchDelegate();

            await storageProvider.WriteJsonObjectAsync(fileName, o);
            return o;
        }

        public static async Task<T> TryRead<T>(this IFileStorageProvider storageProvider, string fileName, T defaultValue) where T : new()
        {
            if (await storageProvider.ExistsAsync(fileName))
                return await storageProvider.ReadJsonObjectAsync<T>(fileName);
            return defaultValue;
        }
    }
}
