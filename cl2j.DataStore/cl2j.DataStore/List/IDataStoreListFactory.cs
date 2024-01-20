using cl2j.DataStore.List;

namespace cl2j.FileStorage.Core
{
    public interface IDataStoreListFactory
    {
        void AddDataStoreListLoad<TValue>(string name, IDataStoreListLoad<TValue> dataStore);
        void AddDataStoreListCommandAndQuery<TKey, TValue>(string name, IDataStoreListCommandAndQuery<TKey, TValue> dataStore);

        IDataStoreListLoad<TValue> GetDataStoreListLoad<TValue>(string name);
        IDataStoreListQuery<TKey, TValue> GetDataStoreListQuery<TKey, TValue>(string name);
        IDataStoreListCommandAndQuery<TKey, TValue> GetDataStoreListCommandAndQuery<TKey, TValue>(string name);
    }
}