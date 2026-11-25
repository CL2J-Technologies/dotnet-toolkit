using cl2j.DataStore.List;
using cl2j.FileStorage.Core;
using cl2j.FileStorage.Extensions;
using Microsoft.Extensions.Logging;

namespace cl2j.DataStore.Json.List
{
    /// <summary>
    ///     A read-only list store backed by one JSON file. <c>jsonOptions</c> lists the types a
    ///     <c>$type</c> may name; none by default. See <see cref="JsonStorageOptions"/>.
    /// </summary>
    public class DataStoreListLoadJson<TValue>(IFileStorageProvider fileStorageProvider, string filename, ILogger logger, JsonStorageOptions? jsonOptions = null) : IDataStoreListLoad<TValue>
    {
        private readonly IFileStorageProvider fileStorageProvider = fileStorageProvider;
        private readonly string filename = filename;
        private readonly ILogger logger = logger;

        public async Task<IReadOnlyList<TValue>> GetAllAsync()
        {
            return await fileStorageProvider.GetListValuesAsync<TValue>(filename, this.logger, jsonOptions);
        }
    }
}
