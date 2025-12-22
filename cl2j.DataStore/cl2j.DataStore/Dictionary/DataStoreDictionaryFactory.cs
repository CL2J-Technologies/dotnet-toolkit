using Microsoft.Extensions.Logging;

namespace cl2j.DataStore.Dictionary
{
    public class DataStoreDictionaryFactory(ILogger<DataStoreDictionaryFactory> logger) : DataStoreDictionaryFactoryBase(logger), IDataStoreDictionaryFactory
    {
        public void AddDataStoreDictionaryCommandAndQuery<TKey, TValue>(string name, IDataStoreDictionaryCommandAndQuery<TKey, TValue> dataStore) where TKey : notnull
        {
            AddDataStore(name, dataStore);
        }

        public IDataStoreDictionaryLoad<TKey, TValue> GetDataStoreDictionaryLoad<TKey, TValue>(string name) where TKey : notnull
        {
            return Get<IDataStoreDictionaryLoad<TKey, TValue>>(name);
        }

        public IDataStoreDictionaryQuery<TKey, TValue> GetDataStoreDictionaryQuery<TKey, TValue>(string name) where TKey : notnull
        {
            return Get<IDataStoreDictionaryQuery<TKey, TValue>>(name);
        }

        public IDataStoreDictionaryCommandAndQuery<TKey, TValue> GetDataStoreDictionaryCommandAndQuery<TKey, TValue>(string name) where TKey : notnull
        {
            return Get<IDataStoreDictionaryCommandAndQuery<TKey, TValue>>(name);
        }
    }
}