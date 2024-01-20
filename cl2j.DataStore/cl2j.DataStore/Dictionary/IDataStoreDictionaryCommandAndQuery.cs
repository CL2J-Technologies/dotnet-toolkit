namespace cl2j.DataStore.Dictionary
{
    public interface IDataStoreDictionaryCommandAndQuery<TKey, TValue> : IDataStoreDictionaryQuery<TKey, TValue> where TKey : notnull
    {
        //Insert a new item. The key must not exists
        Task InsertAsync(TKey key, TValue entity);

        //Update an item
        Task UpdateAsync(TKey key, TValue entity);

        //Delete an item
        Task DeleteAsync(TKey key);

        Task ReplaceAllByAsync(Dictionary<TKey, TValue> items);
    }
}