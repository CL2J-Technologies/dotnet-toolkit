using System.Diagnostics;
using cl2j.DataStore.List;
using cl2j.FileStorage;
using cl2j.FileStorage.Core;
using cl2j.FileStorage.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace cl2j.DataStore.Json.List
{
    public static class DataStoreListJsonExtensions
    {
        //Every method that builds a store takes a trailing jsonOptions: the types a $type may name, for
        //an entity with polymorphic members. None by default, which refuses every $type and makes a
        //polymorphic value throw instead of losing its derived properties. See JsonStorageOptions.

        public static void CreateAndAddDataStoreListLoadJsonWithCache<TValue>(this IServiceProvider builder, string name, string fileStorageName, string dataStoreFileName, TimeSpan refreshInterval, JsonStorageOptions? jsonOptions = null)
        {
            var factory = builder.GetRequiredService<IDataStoreListFactory>();
            var dataStore = builder.CreateDataStoreListLoadJsonWithCache<TValue>(name, fileStorageName, dataStoreFileName, refreshInterval, jsonOptions);
            factory.AddDataStoreListLoad(name, dataStore);
        }

        public static DataStoreListLoadCache<TValue> CreateDataStoreListLoadJsonWithCache<TValue>(this IServiceProvider builder, string cacheName, string fileStorageName, string dataStoreFileName, TimeSpan refreshInterval, JsonStorageOptions? jsonOptions = null)
        {
            var logger = builder.GetRequiredService<ILogger<DataStoreListLoadCache<TValue>>>();
            var fileStorageProvider = builder.GetFileStorageProvider(fileStorageName);
            var dataStore = new DataStoreListLoadJson<TValue>(fileStorageProvider, dataStoreFileName, logger, jsonOptions);
            var dataStoreCache = new DataStoreListLoadCache<TValue>(cacheName, dataStore, refreshInterval, logger);

            return dataStoreCache;
        }

        public static void CreateAndAddDataStoreListCommandAndQueryJsonWithCache<TKey, TValue>(this IServiceProvider builder, string name, string fileStorageName, string dataStoreFileName, TimeSpan refreshInterval, JsonStorageOptions? jsonOptions = null) where TValue : IEntity<TKey>
        {
            var factory = builder.GetRequiredService<IDataStoreListFactory>();
            var dataStore = builder.CreateDataStoreListCommandAndQueryJsonWithCache<TKey, TValue>(name, fileStorageName, dataStoreFileName, refreshInterval, jsonOptions);
            factory.AddDataStoreListCommandAndQuery(name, dataStore);
        }

        public static IDataStoreListCommandAndQuery<TKey, TValue> CreateDataStoreListCommandAndQueryJson<TKey, TValue>(this IServiceProvider builder, string fileStorageName, string dataStoreFileName, JsonStorageOptions? jsonOptions = null) where TValue : IEntity<TKey>
        {
            return builder.CreateDataStoreListCommandAndQueryJson<TKey, TValue>(fileStorageName, dataStoreFileName, r => r.Id, jsonOptions);
        }

        public static IDataStoreListCommandAndQuery<TKey, TValue> CreateDataStoreListCommandAndQueryJson<TKey, TValue>(this IServiceProvider builder, string fileStorageName, string dataStoreFileName, Func<TValue, TKey> predicate, JsonStorageOptions? jsonOptions = null) where TValue : IEntity<TKey>
        {
            var logger = builder.GetRequiredService<ILogger<DataStoreListCommandAndQueryCache<TKey, TValue>>>();
            var fileStorageProvider = builder.GetFileStorageProvider(fileStorageName);
            var dataStore = new DataStoreListCommandAndQueryJson<TKey, TValue>(fileStorageProvider, dataStoreFileName, predicate, logger, jsonOptions: jsonOptions);
            return dataStore;
        }

        public static DataStoreListCommandAndQueryCache<TKey, TValue> CreateDataStoreListCommandAndQueryJsonWithCache<TKey, TValue>(this IServiceProvider builder, string cacheName, string fileStorageName, string dataStoreFileName, TimeSpan refreshInterval, JsonStorageOptions? jsonOptions = null) where TValue : IEntity<TKey>
        {
            return builder.CreateDataStoreListCommandAndQueryJsonWithCache<TKey, TValue>(cacheName, fileStorageName, dataStoreFileName, r => r.Id, refreshInterval, jsonOptions);
        }

        public static DataStoreListCommandAndQueryCache<TKey, TValue> CreateDataStoreListCommandAndQueryJsonWithCache<TKey, TValue>(this IServiceProvider builder, string cacheName, string fileStorageName, string dataStoreFileName, Func<TValue, TKey> predicate, TimeSpan refreshInterval, JsonStorageOptions? jsonOptions = null) where TValue : IEntity<TKey>
        {
            var logger = builder.GetRequiredService<ILogger<DataStoreListCommandAndQueryCache<TKey, TValue>>>();
            var dataStore = builder.CreateDataStoreListCommandAndQueryJson<TKey, TValue>(fileStorageName, dataStoreFileName, jsonOptions);
            var dataStoreCache = new DataStoreListCommandAndQueryCache<TKey, TValue>(cacheName, dataStore, refreshInterval, predicate, logger);
            return dataStoreCache;
        }

        public static async Task<List<TValue>> GetListValuesAsync<TValue>(this IFileStorageProvider fileStorageProvider, string filename, ILogger logger, JsonStorageOptions? jsonOptions = null)
        {
            var sw = Stopwatch.StartNew();
            var list = await fileStorageProvider.ReadJsonObjectAsync<List<TValue>>(filename, jsonOptions);
            if (logger.IsEnabled(LogLevel.Trace))
                logger.LogTrace($"GetListValues<{filename}>() -> {list.Count} in {sw.ElapsedMilliseconds}ms");
            return list;
        }

        public static async Task WriteListValuesAsync<TValue>(this IFileStorageProvider fileStorageProvider, string filename, List<TValue> values, ILogger logger, JsonStorageOptions? jsonOptions = null)
        {
            var sw = Stopwatch.StartNew();
            await fileStorageProvider.WriteJsonObjectAsync(filename, values, jsonOptions);
            if (logger.IsEnabled(LogLevel.Trace))
                logger.LogTrace($"WriteListValuesAsync<{filename}>() -> {values.Count} in {sw.ElapsedMilliseconds}ms");
        }
    }
}