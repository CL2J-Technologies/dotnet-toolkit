using cl2j.Database.Descriptors;

namespace cl2j.Database.CommandBuilders
{
    public interface IIdentifierGenerator
    {
        string GenerateKey(ColumnDescriptor columnDescriptor);
    }
}
