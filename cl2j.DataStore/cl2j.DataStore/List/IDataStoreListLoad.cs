namespace cl2j.DataStore.List
{
    public interface IDataStoreListLoad<TValue>
    {
        //Retreive all the items
        Task<IReadOnlyList<TValue>> GetAllAsync();
    }
}