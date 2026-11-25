using System.Diagnostics;
using cl2j.DataStore.Dictionary;
using cl2j.FileStorage;
using cl2j.FileStorage.Core;
using cl2j.FileStorage.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace cl2j.DataStore.Json.Dictionary
{
    public static class DataStoreDictionaryJsonExtensions
    {
        //Every method that builds a store takes a trailing jsonOptions: the types a $type may name, for
        //an entity with polymorphic members. None by default, which refuses every $type and makes a
        //polymorphic value throw instead of losing its derived properties. See JsonStorageOptions.

        public static void CreateAndAddDataStoreDictionaryJson<TKey, TValue>(this IServiceProvider builder, string name, string fileStorageName, string dataStoreFileName, JsonStorageOptions? jsonOptions = null) where TKey : notnull
        {
            var factory = builder.GetRequiredService<IDataStoreDictionaryFactory>();
            var dataStore = builder.CreateDataStoreDictionaryJson<TKey, TValue>(fileStorageName, dataStoreFileName, jsonOptions);
            factory.AddDataStoreDictionaryCommandAndQuery(name, dataStore);
        }

        public static void CreateAndAddDataStoreDictionaryJsonWithCache<TKey, TValue>(this IServiceProvider builder, string name, string fileStorageName, string dataStoreFileName, TimeSpan refreshInterval, JsonStorageOptions? jsonOptions = null) where TKey : notnull
        {
            var factory = builder.GetRequiredService<IDataStoreDictionaryFactory>();
            var dataStore = builder.CreateDataStoreDictionaryJsonWithCache<TKey, TValue>(name, fileStorageName, dataStoreFileName, refreshInterval, jsonOptions);
            factory.AddDataStoreDictionaryCommandAndQuery(name, dataStore);
        }

        public static DataStoreDictionaryCommandAndQueryJson<TKey, TValue> CreateDataStoreDictionaryJson<TKey, TValue>(this IServiceProvider builder, string fileStorageName, string dataStoreFileName, JsonStorageOptions? jsonOptions = null) where TKey : notnull
        {
            var logger = builder.GetRequiredService<ILogger<DataStoreDictionaryCommandAndQueryJson<TKey, TValue>>>();
            var fileStorageProvider = builder.GetFileStorageProvider(fileStorageName);
            var dataStore = new DataStoreDictionaryCommandAndQueryJson<TKey, TValue>(fileStorageProvider, dataStoreFileName, logger, jsonOptions: jsonOptions);
            return dataStore;
        }

        public static DataStoreDictionaryCommandAndQueryCache<TKey, TValue> CreateDataStoreDictionaryJsonWithCache<TKey, TValue>(this IServiceProvider builder, string cacheName, string fileStorageName, string dataStoreFileName, TimeSpan refreshInterval, JsonStorageOptions? jsonOptions = null) where TKey : notnull
        {
            var logger = builder.GetRequiredService<ILogger<DataStoreDictionaryLoadCache<TKey, TValue>>>();
            var dataStore = builder.CreateDataStoreDictionaryJson<TKey, TValue>(fileStorageName, dataStoreFileName, jsonOptions);
            var dataStoreCache = new DataStoreDictionaryCommandAndQueryCache<TKey, TValue>(cacheName, dataStore, refreshInterval, logger);
            return dataStoreCache;
        }

        public static async Task<Dictionary<TKey, TValue>> GetDictionaryValuesAsync<TKey, TValue>(this IFileStorageProvider fileStorageProvider, string filename, ILogger logger, JsonStorageOptions? jsonOptions = null) where TKey : notnull
        {
            var sw = Stopwatch.StartNew();
            var dict = await fileStorageProvider.ReadJsonObjectAsync<Dictionary<TKey, TValue>>(filename, jsonOptions);
            if (logger.IsEnabled(LogLevel.Trace))
                logger.LogTrace($"GetDictionaryValues<{filename}>() -> {dict.Count} in {sw.ElapsedMilliseconds}ms");
            return dict;
        }
    }
}