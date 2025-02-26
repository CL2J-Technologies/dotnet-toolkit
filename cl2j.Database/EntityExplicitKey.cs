using Dapper.Contrib.Extensions;

namespace cl2j.Database
{
    public class EntityExplicitKey<TKey>
    {
        [ExplicitKey]
        public TKey Id { get; set; } = default!;
    }
}
