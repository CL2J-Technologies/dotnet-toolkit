using System.Data.Common;
using cl2j.Database.Databases;
using Microsoft.Extensions.Logging;

namespace cl2j.Database.IntegrationTests
{
    /// <summary>
    ///     What a provider's test fixture supplies to the shared integration suite.
    ///
    ///     <para>
    ///     The shared tests (<c>XxxTestsBase</c>) never name a provider. They reach the server
    ///     through this interface, and a provider test project makes its own fixture (the one that
    ///     starts the container) implement it. See <see cref="TestSql"/> for how raw SQL in the
    ///     tests is written once and adapted to the provider's identifier quoting.
    ///     </para>
    ///
    ///     <para>
    ///     To add a provider: implement this interface on the fixture, then for each
    ///     <c>XxxTestsBase</c> write
    ///     <c>[Collection(...)] public class XxxTests(MyFixture fixture) : XxxTestsBase(fixture)</c>
    ///     and override only the <c>protected virtual</c> members whose default (the SQL Server
    ///     value) does not hold for the provider.
    ///     </para>
    ///
    ///     <para>
    ///     A derived class may hand its base a different <see cref="IDatabaseFixture"/> than the
    ///     shared one, such as a thin adapter whose <see cref="OpenAsync"/> opens connections with
    ///     another connection option over the same container. That is how a provider runs the same
    ///     tests under two configurations (MySQL's two bulk insert strategies, for one) without the
    ///     shared suite knowing about either.
    ///     </para>
    /// </summary>
    public interface IDatabaseFixture
    {
        /// <summary>
        ///     The connection string of the running test server. Must be usable once the fixture's
        ///     <c>InitializeAsync</c> has completed.
        /// </summary>
        string ConnectionString { get; }

        /// <summary>
        ///     A new open connection to the test server. Each test gets its own rather than
        ///     sharing one, so a test that leaves a transaction open cannot poison the next. The
        ///     caller disposes it. The connection must be one the provider's command builder
        ///     supports (<see cref="CommandBuilders.CommandBuilderFactory"/>), which means the
        ///     provider has registered it before the first test runs.
        /// </summary>
        Task<DbConnection> OpenAsync();

        /// <summary>
        ///     The provider's <see cref="IDatabase"/> implementation, built the way the README
        ///     builds it: over <see cref="ConnectionString"/>, with the given options and logger.
        /// </summary>
        /// <param name="options">Passed through unchanged. Tests set <c>ExceptionLevel</c>.</param>
        /// <param name="logger">Passed through unchanged. Tests read what it captures.</param>
        IDatabase CreateDatabase(DatabaseOptions options, ILogger logger);

        /// <summary>
        ///     The exact type of the connection <see cref="OpenAsync"/> returns and of the one the
        ///     provider's <see cref="IDatabase.CreateConnection"/> returns, for instance
        ///     <c>typeof(SqlConnection)</c>. Compared by exact type, not assignability.
        /// </summary>
        Type ConnectionType { get; }

        /// <summary>
        ///     The exact type of the exception the provider's driver throws for a statement the
        ///     server rejects (a missing table, a failed drop), for instance
        ///     <c>typeof(SqlException)</c>. Compared by exact type, not assignability, so a
        ///     provider whose driver throws a subclass must name the subclass.
        /// </summary>
        Type ServerExceptionType { get; }

        /// <summary>
        ///     A SQL fragment appended to the <c>WHERE</c> clause of the tests' queries on
        ///     <c>INFORMATION_SCHEMA.COLUMNS</c>, narrowing them to the database under test. It
        ///     starts with <c>AND</c> or is empty. SQL Server answers for its one database and
        ///     needs nothing, so its value is empty. MySQL, where the catalog spans every schema,
        ///     returns <c>" AND TABLE_SCHEMA = DATABASE()"</c>; PostgreSQL
        ///     <c>" AND TABLE_SCHEMA = current_schema()"</c>.
        /// </summary>
        string CatalogScopeFilter { get; }
    }
}
