using cl2j.Database.CommandBuilders;

namespace cl2j.Database.PostgreSql
{
    public static class PostgreSql
    {
        public static void Register(IIdentifierGenerator? identifierGenerator = null)
        {
            CommandBuilderFactory.Register(new PostgreSqlCommandBuilder(identifierGenerator));
        }
    }
}
