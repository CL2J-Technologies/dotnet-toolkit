using cl2j.Database.CommandBuilders;

namespace cl2j.Database.SqlServer
{
    public static class SqlServer
    {
        public static void Register(IIdentifierGenerator? identifierGenerator = null)
        {
            CommandBuilderFactory.Register(new SqlServerCommandBuilder(identifierGenerator));
        }
    }
}
