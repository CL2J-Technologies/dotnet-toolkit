namespace cl2j.DataStore
{
    public class Entity<TKey> : IEntity<TKey>
    {
        public TKey Id { get; set; } = default!;
    }
}
