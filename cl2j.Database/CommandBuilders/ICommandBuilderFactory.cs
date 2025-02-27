using System.Data.Common;

namespace cl2j.Database.CommandBuilders
{
    public interface ICommandBuilderFactory
    {
        ICommandBuilder GetCommandBuilder(DbConnection connection);
    }
}