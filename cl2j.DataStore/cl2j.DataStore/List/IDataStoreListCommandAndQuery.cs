namespace cl2j.DataStore.List
{
    public interface IDataStoreListCommandAndQuery<TKey, TValue> : IDataStoreListQuery<TKey, TValue>
    {
        //Insert a new item. The key must not exists
        Task InsertAsync(TValue entity);

        //Update an item
        Task UpdateAsync(TValue entity);

        //Delete an item
        Task DeleteAsync(TKey key);

        Task ReplaceAllByAsync(ICollection<TValue> items);
    }
}