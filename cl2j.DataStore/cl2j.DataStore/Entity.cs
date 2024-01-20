namespace cl2j.DataStore.Core
{
    public class Entity<TKey> : IEntity<TKey>
    {
        public TKey Id { get; set; } = default!;
    }
}
