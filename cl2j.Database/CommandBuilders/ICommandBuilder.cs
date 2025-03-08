using System.Data.Common;
using cl2j.Database.Descriptors;

namespace cl2j.Database.CommandBuilders
{
    public interface ICommandBuilder
    {
        bool Support(DbConnection connection);

        IDatabaseFormatter DatabaseFormatter { get; }

        TextStatement GetTableExistsStatement(Type type);
        TextStatement GetDropTableStatement(Type type);
        TextStatement GetCreateTableStatement(Type type);
        TextStatement GetInsertStatement(Type type);
        TextStatement GetUpdateStatement(Type type);
        TextStatement GetDeleteStatement(Type type);
        TextStatement GetQueryStatement(Type type);

        Task InsertBatch<TIn>(DbConnection connection, IEnumerable<TIn> items, CancellationToken cancellationToken, DbTransaction? transaction = null);
    }
}
