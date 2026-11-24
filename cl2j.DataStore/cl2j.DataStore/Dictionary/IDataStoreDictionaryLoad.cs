namespace cl2j.DataStore.Dictionary
{
    public interface IDataStoreDictionaryLoad<TKey, TValue> where TKey : notnull
    {
        //Retrieve all the items
        Task<IReadOnlyDictionary<TKey, TValue>> GetAllAsync();
    }
}