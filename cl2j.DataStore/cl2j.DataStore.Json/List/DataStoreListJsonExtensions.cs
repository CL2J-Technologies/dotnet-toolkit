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
        public static void CreateAndAddDataStoreListLoadJsonWithCache<TValue>(this IServiceProvider builder, string name, string fileStorageName, string dataStoreFileName, TimeSpan refreshInterval)
        {
            var factory = builder.GetRequiredService<IDataStoreListFactory>();
            var dataStore = builder.CreateDataStoreListLoadJsonWithCache<TValue>(name, fileStorageName, dataStoreFileName, refreshInterval);
            factory.AddDataStoreListLoad(name, dataStore);
        }

        public static DataStoreListLoadCache<TValue> CreateDataStoreListLoadJsonWithCache<TValue>(this IServiceProvider builder, string cacheName, string fileStorageName, string dataStoreFileName, TimeSpan refreshInterval)
        {
            var logger = builder.GetRequiredService<ILogger<DataStoreListLoadCache<TValue>>>();
            var fileStorageProvider = builder.GetFileStorageProvider(fileStorageName);
            var dataStore = new DataStoreListLoadJson<TValue>(fileStorageProvider, dataStoreFileName, logger);
            var dataStoreCache = new DataStoreListLoadCache<TValue>(cacheName, dataStore, refreshInterval, logger);

            return dataStoreCache;
        }

        public static void CreateAndAddDataStoreListCommandAndQueryJsonWithCache<TKey, TValue>(this IServiceProvider builder, string name, string fileStorageName, string dataStoreFileName, TimeSpan refreshInterval) where TValue : IEntity<TKey>
        {
            var factory = builder.GetRequiredService<IDataStoreListFactory>();
            var dataStore = builder.CreateDataStoreListCommandAndQueryJsonWithCache<TKey, TValue>(name, fileStorageName, dataStoreFileName, refreshInterval);
            factory.AddDataStoreListCommandAndQuery(name, dataStore);
        }

        public static IDataStoreListCommandAndQuery<TKey, TValue> CreateDataStoreListCommandAndQueryJson<TKey, TValue>(this IServiceProvider builder, string fileStorageName, string dataStoreFileName) where TValue : IEntity<TKey>
        {
            return builder.CreateDataStoreListCommandAndQueryJson<TKey, TValue>(fileStorageName, dataStoreFileName, r => r.Id);
        }

        public static IDataStoreListCommandAndQuery<TKey, TValue> CreateDataStoreListCommandAndQueryJson<TKey, TValue>(this IServiceProvider builder, string fileStorageName, string dataStoreFileName, Func<TValue, TKey> predicate) where TValue : IEntity<TKey>
        {
            var logger = builder.GetRequiredService<ILogger<DataStoreListCommandAndQueryCache<TKey, TValue>>>();
            var fileStorageProvider = builder.GetFileStorageProvider(fileStorageName);
            var dataStore = new DataStoreListCommandAndQueryJson<TKey, TValue>(fileStorageProvider, dataStoreFileName, predicate, logger);
            return dataStore;
        }

        public static DataStoreListCommandAndQueryCache<TKey, TValue> CreateDataStoreListCommandAndQueryJsonWithCache<TKey, TValue>(this IServiceProvider builder, string cacheName, string fileStorageName, string dataStoreFileName, TimeSpan refreshInterval) where TValue : IEntity<TKey>
        {
            return builder.CreateDataStoreListCommandAndQueryJsonWithCache<TKey, TValue>(cacheName, fileStorageName, dataStoreFileName, r => r.Id, refreshInterval);
        }

        public static DataStoreListCommandAndQueryCache<TKey, TValue> CreateDataStoreListCommandAndQueryJsonWithCache<TKey, TValue>(this IServiceProvider builder, string cacheName, string fileStorageName, string dataStoreFileName, Func<TValue, TKey> predicate, TimeSpan refreshInterval) where TValue : IEntity<TKey>
        {
            var logger = builder.GetRequiredService<ILogger<DataStoreListCommandAndQueryCache<TKey, TValue>>>();
            var dataStore = builder.CreateDataStoreListCommandAndQueryJson<TKey, TValue>(fileStorageName, dataStoreFileName);
            var dataStoreCache = new DataStoreListCommandAndQueryCache<TKey, TValue>(cacheName, dataStore, refreshInterval, predicate, logger);
            return dataStoreCache;
        }

        public static async Task<List<TValue>> GetListValuesAsync<TValue>(this IFileStorageProvider fileStorageProvider, string filename, ILogger logger)
        {
            var sw = Stopwatch.StartNew();
            var list = await fileStorageProvider.ReadJsonObjectAsync<List<TValue>>(filename, null);
            logger.LogTrace($"GetListValues<{filename}>() -> {list.Count} in {sw.ElapsedMilliseconds}ms");
            return list;
        }
    }
}