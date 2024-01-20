using cl2j.FileStorage.Core;
using Microsoft.Extensions.Logging;

namespace cl2j.DataStore.List
{
    public class DataStoreListFactory : DataStoreDictionaryFactoryBase, IDataStoreListFactory
    {
        public DataStoreListFactory(ILogger<DataStoreListFactory> logger)
            : base(logger)
        {
        }

        public void AddDataStoreListLoad<TValue>(string name, IDataStoreListLoad<TValue> dataStore)
        {
            AddDataStore(name, dataStore);
        }

        public void AddDataStoreListCommandAndQuery<TKey, TValue>(string name, IDataStoreListCommandAndQuery<TKey, TValue> dataStore)
        {
            AddDataStore(name, dataStore);
        }

        public IDataStoreListLoad<TValue> GetDataStoreListLoad<TValue>(string name)
        {
            return Get<IDataStoreListLoad<TValue>>(name);
        }

        public IDataStoreListQuery<TKey, TValue> GetDataStoreListQuery<TKey, TValue>(string name)
        {
            return Get<IDataStoreListQuery<TKey, TValue>>(name);
        }

        public IDataStoreListCommandAndQuery<TKey, TValue> GetDataStoreListCommandAndQuery<TKey, TValue>(string name)
        {
            return Get<IDataStoreListCommandAndQuery<TKey, TValue>>(name);
        }
    }
}