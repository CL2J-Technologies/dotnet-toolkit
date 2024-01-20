using cl2j.DataStore.Dictionary;
using cl2j.DataStore.List;
using cl2j.FileStorage.Core;
using Microsoft.Extensions.DependencyInjection;

namespace cl2j.DataStore
{
    public static class DataStoreExtensions
    {
        public static void AddDataStore(this IServiceCollection services)
        {
            services.AddSingleton<IDataStoreListFactory, DataStoreListFactory>();
            services.AddSingleton<IDataStoreDictionaryFactory, DataStoreDictionaryFactory>();
        }
    }
}
