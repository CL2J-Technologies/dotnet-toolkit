namespace cl2j.DataStore.Dictionary
{
    public interface IDataStoreDictionaryLoad<TKey, TValue> where TKey : notnull
    {
        //Retreive all the items
        Task<Dictionary<TKey, TValue>> GetAllAsync();
    }
}