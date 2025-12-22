using cl2j.DataStore.List;
using cl2j.FileStorage.Core;
using Microsoft.Extensions.Logging;

namespace cl2j.DataStore.Json.List
{
    public class DataStoreListLoadJson<TValue>(IFileStorageProvider fileStorageProvider, string filename, ILogger logger) : IDataStoreListLoad<TValue>
    {
        private readonly IFileStorageProvider fileStorageProvider = fileStorageProvider;
        private readonly string filename = filename;
        private readonly ILogger logger = logger;

        public async Task<List<TValue>> GetAllAsync()
        {
            return await fileStorageProvider.GetListValuesAsync<TValue>(filename, this.logger);
        }
    }
}