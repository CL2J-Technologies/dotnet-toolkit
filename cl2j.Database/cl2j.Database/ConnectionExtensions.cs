using System.Data;
using System.Data.Common;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using cl2j.Database.CommandBuilders;
using cl2j.Database.Databases;
using cl2j.Database.Descriptors;
using cl2j.Database.Exceptions;
using cl2j.Database.Helpers;
using cl2j.Tooling.Exceptions;
using Microsoft.Extensions.Logging;

namespace cl2j.Database
{
    public static class ConnectionExtensions
    {
        public static ILogger? Logger { get; set; }
        public static DatabaseOptions DatabaseOptions { get; set; } = new();

        public static readonly JsonSerializerOptions JsonSerializeOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        #region Helpers

        public static string ToJsonString<T>(T value)
        {
            return JsonSerializer.Serialize(value, JsonSerializeOptions);
        }

        #endregion

        #region DDL

        public static async Task<bool> TableExists<T>(this DbConnection connection)
            => await TableExists(connection, typeof(T), CancellationToken.None);

        public static async Task<bool> TableExists(this DbConnection connection, Type type, CancellationToken cancellationToken, DbTransaction? transaction = null)
        {
            await EnsureConnectionOpen(connection, cancellationToken);

            var commandBuilder = CommandBuilderFactory.GetCommandBuilder(connection);
            var statement = commandBuilder.GetTableExistsStatement(type);
            Trace(statement.Text);

            try
            {
                await using var cmd = CreateExecuteCommand(connection, statement.Text, transaction);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static async Task CreateTable<T>(this DbConnection connection)
            => await CreateTable(connection, typeof(T), CancellationToken.None);

        public static async Task CreateTable(this DbConnection connection, Type type, CancellationToken cancellationToken, DbTransaction? transaction = null)
        {
            await EnsureConnectionOpen(connection, cancellationToken);

            var commandBuilder = CommandBuilderFactory.GetCommandBuilder(connection);
            var statement = commandBuilder.GetCreateTableStatement(type);
            Trace(statement.Text);

            await using var cmd = CreateExecuteCommand(connection, statement.Text, transaction);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        public static async Task CreateTableIfRequired<T>(this DbConnection connection)
        {
            if (!await connection.TableExists<T>())
                await CreateTable(connection, typeof(T), CancellationToken.None);
        }

        public static async Task DropTable<T>(this DbConnection connection)
            => await DropTable(connection, typeof(T), CancellationToken.None);

        public static async Task DropTable(this DbConnection connection, Type type, CancellationToken cancellationToken, DbTransaction? transaction = null)
        {
            await EnsureConnectionOpen(connection, cancellationToken);

            var commandBuilder = CommandBuilderFactory.GetCommandBuilder(connection);
            var statement = commandBuilder.GetDropTableStatement(type);
            Trace(statement.Text);

            try
            {
                await using var cmd = CreateExecuteCommand(connection, statement.Text, transaction);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
            catch
            {
            }
        }

        public static async Task DropTableIfExists<T>(this DbConnection connection)
            => await DropTableIfExists(connection, typeof(T), CancellationToken.None);

        public static async Task DropTableIfExists(this DbConnection connection, Type type, CancellationToken cancellationToken, DbTransaction? transaction = null)
        {
            await EnsureConnectionOpen(connection, cancellationToken);

            if (await connection.TableExists(type, cancellationToken, transaction))
                await DropTable(connection, type, cancellationToken, transaction);
        }

        #endregion DDL

        #region Commands

        public static async Task<string> NewKey<T>(this DbConnection connection)
        {
            await EnsureConnectionOpen(connection, CancellationToken.None);

            var commandBuilder = CommandBuilderFactory.GetCommandBuilder(connection);
            var tableDescriptor = TableDescriptorFactory.Create(typeof(T), commandBuilder.DatabaseFormatter);

            var key = tableDescriptor.Keys.FirstOrDefault(k => k.ColumnAtribute.Key == DataAnnotations.KeyType.SelfGeneratedKey) ?? throw new DatabaseException($"No autogenerated key defined for '{typeof(T).Name}'");

            var id = commandBuilder.IdentifierGenerator.GenerateKey(key);
            return id;
        }

        public static async Task Insert<TIn>(this DbConnection connection, TIn item)
            => await Insert(connection, item, CancellationToken.None);

        public static async Task<TOut?> Insert<TIn, TOut>(this DbConnection connection, TIn item)
        {
            var result = await Insert(connection, item, CancellationToken.None);
            if (result is null)
                return default;
            return (TOut)result;
        }

        public static async Task<object?> Insert<TIn>(this DbConnection connection, TIn item, CancellationToken cancellationToken, DbTransaction? transaction = null)
        {
            await EnsureConnectionOpen(connection, cancellationToken);

            var commandBuilder = CommandBuilderFactory.GetCommandBuilder(connection);
            var statement = commandBuilder.GetInsertStatement(typeof(TIn));

            //Generate key value that requires auto-generation
            GenerateKeys(item, statement.TableDescriptor.Keys, commandBuilder.IdentifierGenerator);

            await using var cmd = CreateExecuteCommand(connection, statement.Text, transaction);
            cmd.CreateParameters(item, statement.TableDescriptor);
            var result = await cmd.ExecuteScalarAsync(cancellationToken);

            Trace($"{statement.Text} --> '{result}'");

            return result;
        }

        public static Task BulkInsert<TIn>(this DbConnection connection, IEnumerable<TIn> items)
            => BulkInsert(connection, items, CancellationToken.None, null);

        public static async Task BulkInsert<TIn>(this DbConnection connection, IEnumerable<TIn> items, CancellationToken cancellationToken, DbTransaction? transaction = null)
        {
            await EnsureConnectionOpen(connection, cancellationToken);

            var commandBuilder = CommandBuilderFactory.GetCommandBuilder(connection);

            var tableDescriptor = TableDescriptorFactory.Create(typeof(TIn), commandBuilder.DatabaseFormatter);

            //Generate key value that requires auto-generation
            var autoGenerateKeys = tableDescriptor.Keys.Where(k => k.ColumnAtribute.Key == DataAnnotations.KeyType.AutoGeneratedKey);
            if (autoGenerateKeys.Any())
            {
                foreach (var item in items)
                    GenerateKeys(item, autoGenerateKeys, commandBuilder.IdentifierGenerator);
            }

            await commandBuilder.BulkInsert(connection, items, cancellationToken, transaction);
        }

        public static async Task Update<TIn>(this DbConnection connection, TIn item)
            => await Update(connection, item, CancellationToken.None);

        public static async Task<object?> Update<TIn>(this DbConnection connection, TIn item, CancellationToken cancellationToken, DbTransaction? transaction = null)
        {
            await EnsureConnectionOpen(connection, cancellationToken);

            var commandBuilder = CommandBuilderFactory.GetCommandBuilder(connection);
            var statement = commandBuilder.GetUpdateStatement(typeof(TIn));

            await using var cmd = CreateExecuteCommand(connection, statement.Text, transaction);
            cmd.CreateParameters(item, statement.TableDescriptor);
            var result = await cmd.ExecuteNonQueryAsync(cancellationToken);

            Trace($"{statement.Text} --> '{result}'");

            return result;
        }

        public static async Task UpdateColumn<TIn, TValue>(this DbConnection connection, TIn item, string columnName, TValue? value)
            => await UpdateColumn(connection, item, columnName, value, CancellationToken.None);

        public static async Task UpdateColumn<TIn, TValue>(this DbConnection connection, TIn item, string columnName, TValue? value, CancellationToken cancellationToken, DbTransaction? transaction = null)
        {
            await EnsureConnectionOpen(connection, cancellationToken);

            var commandBuilder = CommandBuilderFactory.GetCommandBuilder(connection);
            var statement = commandBuilder.GetUpdateColumnStatement(typeof(TIn), columnName);

            await using var cmd = CreateExecuteCommand(connection, statement.Text, transaction);
            cmd.CreateParameters(item, statement.TableDescriptor, columnName, value);
            var result = await cmd.ExecuteNonQueryAsync(cancellationToken);

            Trace($"{statement.Text} --> '{result}'");
        }

        public static async Task Delete<TIn>(this DbConnection connection, TIn item)
           => await Delete(connection, item, CancellationToken.None);

        public static async Task<object?> Delete<TIn>(this DbConnection connection, TIn item, CancellationToken cancellationToken, DbTransaction? transaction = null)
        {
            await EnsureConnectionOpen(connection, cancellationToken);

            var commandBuilder = CommandBuilderFactory.GetCommandBuilder(connection);
            var statement = commandBuilder.GetDeleteStatement(typeof(TIn));

            await using var cmd = CreateExecuteCommand(connection, statement.Text, transaction);
            cmd.CreateKeyParameters(item, statement.TableDescriptor);
            var result = await cmd.ExecuteNonQueryAsync(cancellationToken);

            Trace($"{statement.Text} --> '{result}'");

            return result;
        }

        public static async Task DeleteKey<TIn>(this DbConnection connection, object key)
           => await DeleteKey<TIn>(connection, key, CancellationToken.None);

        public static async Task<object?> DeleteKey<TIn>(this DbConnection connection, object key, CancellationToken cancellationToken, DbTransaction? transaction = null)
        {
            await EnsureConnectionOpen(connection, cancellationToken);

            var commandBuilder = CommandBuilderFactory.GetCommandBuilder(connection);
            var statement = commandBuilder.GetDeleteStatement(typeof(TIn));

            await using var cmd = CreateExecuteCommand(connection, statement.Text, transaction);
            cmd.CreateKeyParameter<TIn>(key, statement.TableDescriptor);
            var result = await cmd.ExecuteNonQueryAsync(cancellationToken);

            Trace($"{statement.Text} --> '{result}'");

            return result;
        }

        public static async Task<int> Execute(this DbConnection connection, string sql, object? param = null)
            => await Execute(connection, sql, param, CancellationToken.None);

        public static async Task<int> Execute(this DbConnection connection, string sql, object? param, CancellationToken cancellationToken, DbTransaction? transaction = null)
        {
            await EnsureConnectionOpen(connection, cancellationToken);

            var commandBuilder = CommandBuilderFactory.GetCommandBuilder(connection);

            await using var cmd = CreateExecuteCommand(connection, sql, transaction);
            if (param is not null)
                cmd.CreateObjectParameters(param, commandBuilder);
            var result = await cmd.ExecuteNonQueryAsync(cancellationToken);
            return result;
        }

        #endregion Commands

        #region Query

        public static string BuildSelectStatement<T>(this DbConnection connection, string? sqlStatement = null)
        {
            var commandBuilder = CommandBuilderFactory.GetCommandBuilder(connection);
            var statement = commandBuilder.GetQueryStatement(typeof(T));
            if (sqlStatement is null)
                return statement.Text;

            return statement.Text + " " + sqlStatement;
        }

        public static async Task<List<T>> Query<T>(this DbConnection connection, object? param = null)
            => await Query<T>(connection, param, CancellationToken.None);

        public static async Task<List<T>> Query<T>(this DbConnection connection, object? param, CancellationToken cancellationToken, DbTransaction? transaction = null)
        {
            var commandBuilder = CommandBuilderFactory.GetCommandBuilder(connection);
            var statement = param is null ? commandBuilder.GetQueryStatement(typeof(T)) : commandBuilder.GetQueryStatement(typeof(T), param.GetType());
            return await Query<T>(connection, statement.Text, param, cancellationToken, transaction);
        }

        public static async Task<List<T>> Query<T>(this DbConnection connection, string sql, object? param = null)
            => await Query<T>(connection, sql, param, CancellationToken.None);

        public static async Task<List<T>> Query<T>(this DbConnection connection, string sql, object? param, CancellationToken cancellationToken, DbTransaction? transaction = null)
        {
            await EnsureConnectionOpen(connection, cancellationToken);

            var commandBuilder = CommandBuilderFactory.GetCommandBuilder(connection);
            var tableDescriptor = TableDescriptorFactory.Create(typeof(T), commandBuilder.DatabaseFormatter);

            await using var cmd = CreateExecuteCommand(connection, sql, transaction);
            if (param is not null)
                cmd.CreateObjectParameters(param, commandBuilder);
            await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleResult | CommandBehavior.SequentialAccess, cancellationToken);

            var results = await reader.Read<T>(tableDescriptor);
            return results;
        }

        public static async Task<T?> QuerySingle<T>(this DbConnection connection, string sql, object? param = null)
            => await QuerySingle<T>(connection, sql, param, CancellationToken.None);

        public static async Task<T?> QuerySingle<T>(this DbConnection connection, string sql, object? param, CancellationToken cancellationToken, DbTransaction? transaction = null)
        {
            await EnsureConnectionOpen(connection, cancellationToken);

            var commandBuilder = CommandBuilderFactory.GetCommandBuilder(connection);
            var tableDescriptor = TableDescriptorFactory.Create(typeof(T), commandBuilder.DatabaseFormatter);

            await using var cmd = CreateExecuteCommand(connection, sql, transaction);
            if (param is not null)
                cmd.CreateObjectParameters(param, commandBuilder);
            await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleResult | CommandBehavior.SingleRow | CommandBehavior.SequentialAccess, cancellationToken);

            return await reader.ReadSingle<T>(tableDescriptor);
        }

        public static async Task<T?> QuerySingle<T>(this DbConnection connection, object param)
            => await QuerySingle<T>(connection, param, CancellationToken.None);

        public static async Task<T?> QuerySingle<T>(this DbConnection connection, object param, CancellationToken cancellationToken, DbTransaction? transaction = null)
        {
            var commandBuilder = CommandBuilderFactory.GetCommandBuilder(connection);
            var statement = commandBuilder.GetQueryStatement(typeof(T), param.GetType());

            await using var cmd = CreateExecuteCommand(connection, statement.Text, transaction);
            cmd.CreateObjectParameters(param, commandBuilder);

            await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleResult | CommandBehavior.SingleRow | CommandBehavior.SequentialAccess, cancellationToken);

            return await reader.ReadSingle<T>(statement.TableDescriptor);
        }

        public static async Task<T?> QueryKey<T>(this DbConnection connection, object key)
            => await QueryKey<T>(connection, key, CancellationToken.None);

        public static async Task<T?> QueryKey<T>(this DbConnection connection, object key, CancellationToken cancellationToken, DbTransaction? transaction = null)
        {
            await EnsureConnectionOpen(connection, cancellationToken);

            var commandBuilder = CommandBuilderFactory.GetCommandBuilder(connection);
            var statement = commandBuilder.GetQueryByKeyStatement(typeof(T));

            await using var cmd = CreateExecuteCommand(connection, statement.Text, transaction);
            cmd.CreateKeyParameter<T>(key, statement.TableDescriptor);
            await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleResult | CommandBehavior.SingleRow | CommandBehavior.SequentialAccess, cancellationToken);

            return await reader.ReadSingle<T>(statement.TableDescriptor);
        }

        public static async Task<List<T>> QueryKeys<T>(this DbConnection connection, IEnumerable<object> keys)
            => await QueryKeys<T>(connection, keys, CancellationToken.None);

        public static async Task<List<T>> QueryKeys<T>(this DbConnection connection, IEnumerable<object> keys, CancellationToken cancellationToken, DbTransaction? transaction = null)
        {
            await EnsureConnectionOpen(connection, cancellationToken);

            var commandBuilder = CommandBuilderFactory.GetCommandBuilder(connection);
            var tableDescriptor = TableDescriptorFactory.Create(typeof(T), commandBuilder.DatabaseFormatter);
            var statement = commandBuilder.GetQueryByKeysStatement(typeof(T), keys);

            //TODO - Split in batches

            await using var cmd = CreateExecuteCommand(connection, statement.Text, transaction);
            await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleResult | CommandBehavior.SequentialAccess, cancellationToken);

            var results = await reader.Read<T>(tableDescriptor);
            return results;
        }

        #endregion

        #region Private

        private static async Task EnsureConnectionOpen(DbConnection connection, CancellationToken cancellationToken)
        {
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync(cancellationToken);
        }

        private static DbCommand CreateExecuteCommand(DbConnection connection, string sql, DbTransaction? transaction)
        {
            var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = sql;
            cmd.CommandType = CommandType.Text;
            return cmd;
        }

        private static void CreateParameters<T>(this DbCommand command, T t, TableDescriptor tableDescriptor)
        {
            CreateParameters(command, t!, tableDescriptor.Columns);
        }

        private static void CreateParameters<T, TValue>(this DbCommand command, T t, TableDescriptor tableDescriptor, string columnName, TValue? value)
        {
            var column = tableDescriptor.Columns.FirstOrDefault(c => c.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase)) ?? throw new ValidationException($"column '{columnName}' doesn't exists");
            CreateParameter(command, column, value);

            //Ensure keys are also added for the where clause
            CreateKeyParameters(command, t!, tableDescriptor);
        }

        private static void CreateObjectParameters(this DbCommand command, object param, ICommandBuilder commandBuilder)
        {
            var type = param.GetType();
            var tableDescriptor = TableDescriptorFactory.Create(type, commandBuilder.DatabaseFormatter);
            command.CreateParameters(param, tableDescriptor.Columns);
        }

        private static void CreateKeyParameters<T>(this DbCommand command, T t, TableDescriptor tableDescriptor)
        {
            var columns = tableDescriptor.Columns.Where(c => c.ColumnAtribute.Key != DataAnnotations.KeyType.None);
            CreateParameters(command, t!, columns);
        }

        private static void CreateKeyParameter<T>(this DbCommand command, object key, TableDescriptor tableDescriptor)
        {
            var columns = tableDescriptor.Columns.Where(c => c.ColumnAtribute.Key != DataAnnotations.KeyType.None);
            if (columns.Count() != 1)
                throw new DatabaseException($"{tableDescriptor.Name} : Must have exactly one key, found {columns.Count()}");

            var column = columns.First();

            var parameter = command.CreateParameter();
            parameter.ParameterName = column.Name;
            parameter.Value = key;
            command.Parameters.Add(parameter);
        }

        private static void CreateParameters(this DbCommand command, object t, IEnumerable<ColumnDescriptor> columns)
        {
            foreach (var column in columns)
                command.CreateParameter(t, column);
        }

        private static void CreateParameter(this DbCommand command, object t, ColumnDescriptor column)
        {
            var value = column.Property.GetValue(t, null);
            command.CreateParameter(column, value);
        }

        private static void CreateParameter(this DbCommand command, ColumnDescriptor column, object? value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = column.Name;

            if (column.ColumnAtribute.Json)
            {
                parameter.Value = ToJsonString(value);
                parameter.DbType = DbType.String;
            }
            else if (value is null && column.Property.PropertyType == Types.TypeDateTimeOffset && string.IsNullOrEmpty(column.ColumnAtribute.Default))
                parameter.Value = DateTimeOffset.UtcNow;
            else
                parameter.Value = value ?? DBNull.Value;

            command.Parameters.Add(parameter);
        }

        private static void GenerateKeys<TIn>(TIn item, IEnumerable<ColumnDescriptor> keys, IIdentifierGenerator identifierGenerator)
        {
            foreach (var key in keys)
            {
                if (key.ColumnAtribute.Key == DataAnnotations.KeyType.AutoGeneratedKey)
                    key.Property.SetValue(item, identifierGenerator.GenerateKey(key));
            }
        }

        private static void Trace(string text) => Logger?.Log(DatabaseOptions.TraceLevel, text);

        #endregion
    }
}
