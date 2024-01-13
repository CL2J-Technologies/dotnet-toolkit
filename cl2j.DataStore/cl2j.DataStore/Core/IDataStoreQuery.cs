namespace cl2j.DataStore
{
    public interface IDataStoreQuery<TKey, TValue>
    {
        //Retreive all the items
        Task<List<TValue>> GetAllAsync();

        //Get an item by it's key (Id)
        Task<TValue?> GetByIdAsync(TKey key);
    }
}