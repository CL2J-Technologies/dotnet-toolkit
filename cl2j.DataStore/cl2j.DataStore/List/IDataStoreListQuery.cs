namespace cl2j.DataStore.List
{
    public interface IDataStoreListQuery<TKey, TValue> : IDataStoreListLoad<TValue>
    {
        //Get an item by it's key (Id)
        Task<TValue?> GetByIdAsync(TKey key);
    }
}