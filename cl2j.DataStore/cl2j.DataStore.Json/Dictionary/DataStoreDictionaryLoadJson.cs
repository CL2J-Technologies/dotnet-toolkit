using cl2j.DataStore.Dictionary;
using cl2j.FileStorage.Core;
using Microsoft.Extensions.Logging;

namespace cl2j.DataStore.Json.Dictionary
{
    public class DataStoreDictionaryLoadJson<TKey, TValue>(IFileStorageProvider fileStorageProvider, string filename, ILogger logger) : IDataStoreDictionaryLoad<TKey, TValue> where TKey : notnull
    {
        public async Task<Dictionary<TKey, TValue>> GetAllAsync()
        {
            return await fileStorageProvider.GetDictionaryValuesAsync<TKey, TValue>(filename, logger);
        }
    }
}