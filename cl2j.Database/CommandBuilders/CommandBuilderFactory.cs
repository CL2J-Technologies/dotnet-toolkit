using System.Data.Common;
using cl2j.Database.Exceptions;

namespace cl2j.Database.CommandBuilders
{
    public class CommandBuilderFactory
    {
        private static readonly List<ICommandBuilder> builders = [];

        public static void Register(ICommandBuilder commandBuilder)
        {
            builders.Add(commandBuilder);
        }

        public static ICommandBuilder GetCommandBuilder(DbConnection connection)
        {
            foreach (var builder in builders)
            {
                if (builder.Support(connection))
                    return builder;
            }

            throw new DatabaseException($"No CommandBuilder registered for connection '{connection.GetType().Name}'");
        }
    }
}
