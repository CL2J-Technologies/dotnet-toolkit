namespace cl2j.DataStore.Dictionary
{
    public interface IDataStoreDictionaryQuery<TKey, TValue> : IDataStoreDictionaryLoad<TKey, TValue> where TKey : notnull
    {
        //Get an item by it's key (Id)
        Task<TValue?> GetByIdAsync(TKey key);
    }
}