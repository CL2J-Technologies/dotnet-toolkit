namespace cl2j.DataStore.Dictionary
{
    public interface IDataStoreDictionaryFactory
    {
        void AddDataStoreDictionaryCommandAndQuery<TKey, TValue>(string name, IDataStoreDictionaryCommandAndQuery<TKey, TValue> dataStore) where TKey : notnull;

        IDataStoreDictionaryLoad<TKey, TValue> GetDataStoreDictionaryLoad<TKey, TValue>(string name) where TKey : notnull;
        IDataStoreDictionaryQuery<TKey, TValue> GetDataStoreDictionaryQuery<TKey, TValue>(string name) where TKey : notnull;
        IDataStoreDictionaryCommandAndQuery<TKey, TValue> GetDataStoreDictionaryCommandAndQuery<TKey, TValue>(string name) where TKey : notnull;
    }
}