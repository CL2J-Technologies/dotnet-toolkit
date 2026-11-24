using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace cl2j.Database.Tests
{
    /// <summary>
    ///     An open connection that talks to nothing. Commands made from it accept parameters, report
    ///     one affected row, return no scalar and read an empty result set with the columns the test
    ///     gives it. It exists so the public paths of <c>ConnectionExtensions</c> can be run end to
    ///     end, as far as the point where a server would be needed.
    /// </summary>
    internal sealed class RecordingDbConnection : DbConnection
    {
        /// <summary>The columns of the empty result set a reader command returns.</summary>
        public DataTable ReaderShape { get; set; } = new();

        /// <summary>What a scalar command returns.</summary>
        public object? ScalarResult { get; set; }

        /// <summary>The commands created from this connection, in order.</summary>
        public List<RecordingDbCommand> Commands { get; } = [];

        [AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;

        public override string Database => string.Empty;
        public override string DataSource => string.Empty;
        public override string ServerVersion => string.Empty;
        public override ConnectionState State => ConnectionState.Open;

        public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();
        public override void Close() { }
        public override void Open() { }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();

        protected override DbCommand CreateDbCommand()
        {
            var command = new RecordingDbCommand(this);
            Commands.Add(command);
            return command;
        }
    }

    internal sealed class RecordingDbCommand(RecordingDbConnection connection) : DbCommand
    {
        private readonly RecordingParameterCollection parameters = new();

        [AllowNull]
        public override string CommandText { get; set; } = string.Empty;

        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; }
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }

        protected override DbConnection? DbConnection { get; set; } = connection;
        protected override DbParameterCollection DbParameterCollection => parameters;
        protected override DbTransaction? DbTransaction { get; set; }

        public override void Cancel() { }
        public override int ExecuteNonQuery() => 1;
        public override object? ExecuteScalar() => connection.ScalarResult;
        public override void Prepare() { }

        protected override DbParameter CreateDbParameter() => new RecordingDbParameter();

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
            => new DataTableReader(connection.ReaderShape);
    }

    internal sealed class RecordingDbParameter : DbParameter
    {
        public override DbType DbType { get; set; } = DbType.String;
        public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;
        public override bool IsNullable { get; set; }

        [AllowNull]
        public override string ParameterName { get; set; } = string.Empty;

        public override int Size { get; set; }

        [AllowNull]
        public override string SourceColumn { get; set; } = string.Empty;

        public override bool SourceColumnNullMapping { get; set; }
        public override object? Value { get; set; }

        public override void ResetDbType() { }
    }

    internal sealed class RecordingParameterCollection : DbParameterCollection
    {
        private readonly List<DbParameter> items = [];

        public override int Count => items.Count;
        public override object SyncRoot => ((ICollection)items).SyncRoot;

        public override int Add(object value)
        {
            items.Add((DbParameter)value);
            return items.Count - 1;
        }

        public override void AddRange(Array values)
        {
            foreach (var value in values)
                Add(value);
        }

        public override void Clear() => items.Clear();
        public override bool Contains(object value) => items.Contains((DbParameter)value);
        public override bool Contains(string value) => IndexOf(value) >= 0;
        public override void CopyTo(Array array, int index) => ((ICollection)items).CopyTo(array, index);
        public override IEnumerator GetEnumerator() => items.GetEnumerator();
        public override int IndexOf(object value) => items.IndexOf((DbParameter)value);
        public override int IndexOf(string parameterName) => items.FindIndex(p => p.ParameterName == parameterName);
        public override void Insert(int index, object value) => items.Insert(index, (DbParameter)value);
        public override void Remove(object value) => items.Remove((DbParameter)value);
        public override void RemoveAt(int index) => items.RemoveAt(index);
        public override void RemoveAt(string parameterName) => items.RemoveAt(IndexOf(parameterName));

        protected override DbParameter GetParameter(int index) => items[index];
        protected override DbParameter GetParameter(string parameterName) => items[IndexOf(parameterName)];
        protected override void SetParameter(int index, DbParameter value) => items[index] = value;
        protected override void SetParameter(string parameterName, DbParameter value) => items[IndexOf(parameterName)] = value;
    }
}
