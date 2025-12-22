using Dapper.Contrib.Extensions;

namespace cl2j.DataStore.Database
{
    public class EntityExplicitKey<TKey> : IEntity<TKey>
    {
        [ExplicitKey]
        public TKey Id { get; set; } = default!;
    }
}
