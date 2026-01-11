using System.Data.Common;
using cl2j.Database.Descriptors;

namespace cl2j.Database.CommandBuilders
{
    public interface ICommandBuilder
    {
        bool Support(DbConnection connection);

        IDatabaseFormatter DatabaseFormatter { get; }
        IIdentifierGenerator IdentifierGenerator { get; }

        TextStatement GetTableExistsStatement(Type type);
        TextStatement GetDropTableStatement(Type type);
        TextStatement GetCreateTableStatement(Type type);

        TextStatement GetInsertStatement(Type type);
        TextStatement GetUpdateStatement(Type type);
        TextStatement GetUpdateColumnStatement(Type type, string columnName);
        TextStatement GetDeleteStatement(Type type);

        TextStatement GetQueryStatement(Type type);
        TextStatement GetQueryStatement(Type type, Type paramType);
        TextStatement GetQueryByKeyStatement(Type type);
        TextStatement GetQueryByKeysStatement(Type type, IEnumerable<object> values);

        Task BulkInsert<TIn>(DbConnection connection, IEnumerable<TIn> items, CancellationToken cancellationToken, DbTransaction? transaction = null);
    }
}
