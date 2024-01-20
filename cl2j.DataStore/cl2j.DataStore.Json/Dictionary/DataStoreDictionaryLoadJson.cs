using cl2j.DataStore.Dictionary;
using cl2j.FileStorage.Core;
using Microsoft.Extensions.Logging;

namespace cl2j.DataStore.Json.Dictionary
{
    public class DataStoreDictionaryLoadJson<TKey, TValue> : IDataStoreDictionaryLoad<TKey, TValue> where TKey : notnull
    {
        private readonly IFileStorageProvider fileStorageProvider;
        private readonly string filename;
        private readonly ILogger logger;

        public DataStoreDictionaryLoadJson(IFileStorageProvider fileStorageProvider, string filename, ILogger logger)
        {
            this.fileStorageProvider = fileStorageProvider;
            this.filename = filename;
            this.logger = logger;
        }

        public async Task<Dictionary<TKey, TValue>> GetAllAsync()
        {
            return await fileStorageProvider.GetDictionaryValuesAsync<TKey, TValue>(filename, logger);
        }
    }
}