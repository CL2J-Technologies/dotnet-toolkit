using cl2j.DataStore.List;
using cl2j.FileStorage.Core;
using Microsoft.Extensions.Logging;

namespace cl2j.DataStore.Json.List
{
    public class DataStoreListLoadJson<TValue> : IDataStoreListLoad<TValue>
    {
        private readonly IFileStorageProvider fileStorageProvider;
        private readonly string filename;
        private readonly ILogger logger;

        public DataStoreListLoadJson(IFileStorageProvider fileStorageProvider, string filename, ILogger logger)
        {
            this.fileStorageProvider = fileStorageProvider;
            this.filename = filename;
            this.logger = logger;
        }

        public async Task<List<TValue>> GetAllAsync()
        {
            return await fileStorageProvider.GetListValuesAsync<TValue>(filename, this.logger);
        }
    }
}