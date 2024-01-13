namespace cl2j.DataStore
{
    public interface IEntity<TKey>
    {
        TKey Id { get; set; }
    }
}
