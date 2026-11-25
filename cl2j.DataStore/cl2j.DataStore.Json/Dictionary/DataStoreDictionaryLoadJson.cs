using cl2j.DataStore.Dictionary;
using cl2j.FileStorage.Core;
using cl2j.FileStorage.Extensions;
using Microsoft.Extensions.Logging;

namespace cl2j.DataStore.Json.Dictionary
{
    /// <summary>
    ///     A read-only dictionary store backed by one JSON file. <c>jsonOptions</c> lists the types
    ///     a <c>$type</c> may name; none by default. See <see cref="JsonStorageOptions"/>.
    /// </summary>
    public class DataStoreDictionaryLoadJson<TKey, TValue>(IFileStorageProvider fileStorageProvider, string filename, ILogger logger, JsonStorageOptions? jsonOptions = null) : IDataStoreDictionaryLoad<TKey, TValue> where TKey : notnull
    {
        public async Task<IReadOnlyDictionary<TKey, TValue>> GetAllAsync()
        {
            return await fileStorageProvider.GetDictionaryValuesAsync<TKey, TValue>(filename, logger, jsonOptions);
        }
    }
}
