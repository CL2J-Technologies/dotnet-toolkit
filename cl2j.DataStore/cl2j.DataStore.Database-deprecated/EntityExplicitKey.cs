using Dapper.Contrib.Extensions;

namespace cl2j.DataStore.Database
{
    [Obsolete("cl2j.DataStore.Database is deprecated. In cl2j.Database a key that the application supplies is declared on the property itself, with [Column(Key = KeyType.Key)], so there is no base class to inherit.")]
    public class EntityExplicitKey<TKey> : IEntity<TKey>
    {
        [ExplicitKey]
        public TKey Id { get; set; } = default!;
    }
}
