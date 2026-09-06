using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace cl2j.Database.Tests
{
    /// <summary>
    ///     The smallest thing that is a <see cref="DbConnection"/> and is not a
    ///     <c>SqlConnection</c>. Nothing on it is ever called: it exists so a resolve can be asked
    ///     for a connection no registered builder supports.
    /// </summary>
    internal sealed class UnsupportedConnection : DbConnection
    {
        //[AllowNull] matches the base declaration, which permits null on the setter.
        [AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;

        public override string Database => string.Empty;
        public override string DataSource => string.Empty;
        public override string ServerVersion => string.Empty;
        public override ConnectionState State => ConnectionState.Closed;

        public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();
        public override void Close() => throw new NotSupportedException();
        public override void Open() => throw new NotSupportedException();

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
        protected override DbCommand CreateDbCommand() => throw new NotSupportedException();
    }
}
