using cl2j.Database.Descriptors;

namespace cl2j.Database.CommandBuilders
{
    public class GuidIdentifierGenerator : IIdentifierGenerator
    {
#pragma warning disable CA2211 // Non-constant fields should not be visible
        public static GuidIdentifierGenerator Default = new();
#pragma warning restore CA2211 // Non-constant fields should not be visible

        public string GenerateKey(ColumnDescriptor columnDescriptor)
        {
            var id = Guid.NewGuid().ToString("N");

            if (columnDescriptor.ColumnAtribute.KeyPrefix is not null)
                id = columnDescriptor.ColumnAtribute.KeyPrefix + id;

            return id;
        }
    }
}
