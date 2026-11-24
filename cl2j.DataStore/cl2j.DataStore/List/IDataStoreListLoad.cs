namespace cl2j.DataStore.List
{
    public interface IDataStoreListLoad<TValue>
    {
        //Retrieve all the items
        Task<IReadOnlyList<TValue>> GetAllAsync();
    }
}