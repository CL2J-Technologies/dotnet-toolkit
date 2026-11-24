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

        /// <summary>
        ///     The most keys a single query-by-keys statement may carry. A longer list is split
        ///     into several statements by the caller.
        ///
        ///     <para>
        ///     Each key is one bound parameter, so this is bounded by the server's parameter limit:
        ///     2100 on SQL Server, 65,535 on PostgreSQL. The default of 2000 fits under the smallest
        ///     of them; override it to go higher where the server allows. Must be greater than zero.
        ///     </para>
        /// </summary>
        int MaxKeysPerStatement => QueryKeyBatches.MaxKeysPerStatement;

        /// <summary>
        ///     Called for every parameter the library binds on a command, once the core has set its
        ///     name, value, <see cref="System.Data.DbType"/>, size and precision, and before the
        ///     command runs. Lets a provider adjust what its driver needs, for instance a
        ///     provider-specific type for a JSON column, or a conversion its driver insists on.
        ///
        ///     <para>
        ///     <paramref name="column"/> is the column the parameter stands for, or <c>null</c> when
        ///     there is none. It is not called by <c>BulkInsert</c>, which writes values itself.
        ///     The default does nothing.
        ///     </para>
        /// </summary>
        void PrepareParameter(DbParameter parameter, ColumnDescriptor? column) { }
    }
}
