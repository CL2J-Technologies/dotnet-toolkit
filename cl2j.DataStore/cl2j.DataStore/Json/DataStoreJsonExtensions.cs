using cl2j.DataStore.Cache;
using cl2j.FileStorage.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace cl2j.DataStore.Json
{
    public static class DataStoreJsonExtensions
    {
        public static void AddDataStoreJsonWithCache<TKey, TValue>(this IServiceCollection services, string fileStorageName, string dataStoreFileName, Func<TValue, TKey> predicate, TimeSpan refreshInterval)
        {
            services.AddSingleton<IDataStore<TKey, TValue>>(builder =>
            {
                var logger = builder.GetRequiredService<ILogger<DataStoreCache<TKey, TValue>>>();
                var fileStorageFactory = builder.GetRequiredService<IFileStorageFactory>();
                var fileStorageProvider = fileStorageFactory.GetProvider(fileStorageName) ?? throw new InvalidOperationException();
                var dataStore = new DataStoreJson<TKey, TValue>(fileStorageProvider, dataStoreFileName, predicate, logger);
                var dataStoreCache = new DataStoreCache<TKey, TValue>(GetName<TValue>(), dataStore, refreshInterval, predicate, logger);

                return dataStoreCache;
            });
        }

        public static void AddDataStoreJson<TKey, TValue>(this IServiceCollection services, string fileStorageName, string dataStoreFileName, Func<TValue, TKey> predicate)
        {
            services.AddSingleton<IDataStore<TKey, TValue>>(builder =>
            {
                var logger = builder.GetRequiredService<ILogger<DataStoreCache<TKey, TValue>>>();
                var fileStorageFactory = builder.GetRequiredService<IFileStorageFactory>();
                var fileStorageProvider = fileStorageFactory.GetProvider(fileStorageName) ?? throw new InvalidOperationException();
                var dataStore = new DataStoreJson<TKey, TValue>(fileStorageProvider, dataStoreFileName, predicate, logger);

                return dataStore;
            });
        }

        private static string GetName<T>()
        {
            return typeof(T).Name.ToLower();
        }
    }
}