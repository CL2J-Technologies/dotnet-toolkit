using cl2j.FileStorage.Core;
using cl2j.FileStorage.Extensions;

namespace cl2j.DataStore.Json
{
    public static class DataStoreJsonHelpers
    {
        //jsonOptions: the types a $type may name; none by default. See JsonStorageOptions.
        public static async Task<T> GetOrFetch<T>(this IFileStorageProvider storageProvider, string fileName, Func<Task<T>> fetchDelegate, JsonStorageOptions? jsonOptions = null) where T : new()
        {
            if (await storageProvider.ExistsAsync(fileName))
                return await storageProvider.ReadJsonObjectAsync<T>(fileName, jsonOptions);

            var o = await fetchDelegate();

            await storageProvider.WriteJsonObjectAsync(fileName, o, jsonOptions);
            return o;
        }

        //jsonOptions: the types a $type may name; none by default. See JsonStorageOptions.
        public static async Task<T> TryRead<T>(this IFileStorageProvider storageProvider, string fileName, T defaultValue, JsonStorageOptions? jsonOptions = null) where T : new()
        {
            if (await storageProvider.ExistsAsync(fileName))
                return await storageProvider.ReadJsonObjectAsync<T>(fileName, jsonOptions);
            return defaultValue;
        }
    }
}
