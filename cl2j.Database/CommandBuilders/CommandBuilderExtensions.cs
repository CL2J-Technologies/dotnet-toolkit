using System.Data.Common;

namespace cl2j.Database.CommandBuilders
{
    public static class CommandBuilderExtensions
    {
        public static ICommandBuilderFactory CommandBuilder { get; set; } = new CommandBuilderFactory();

        public static ICommandBuilder GetCommandBuilder(this DbConnection connection)
        {
            return CommandBuilder.GetCommandBuilder(connection);
        }
    }
}
