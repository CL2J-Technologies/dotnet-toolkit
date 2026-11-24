using System.Data.Common;
using System.Text.RegularExpressions;
using cl2j.Database.CommandBuilders;

namespace cl2j.Database.IntegrationTests
{
    /// <summary>
    ///     Raw SQL in the shared tests is written once, with SQL Server's <c>[Name]</c> quoting,
    ///     and passed through here to take the provider's quoting. <c>[Schema].[Name]</c> works
    ///     too: each bracketed part is rewritten on its own.
    ///
    ///     <para>
    ///     The quoting comes from the provider's own formatter
    ///     (<c>ICommandBuilder.DatabaseFormatter.FormatColumnName</c>), the one the library uses
    ///     to build its statements, so a test that compares the library's output with an expected
    ///     string and a test that hand-writes a statement both agree with the provider by
    ///     construction. For SQL Server it is the identity.
    ///     </para>
    ///
    ///     <para>
    ///     Test code only. It rewrites every bracketed word, including one inside a string
    ///     literal, which the tests never contain.
    ///     </para>
    /// </summary>
    public static partial class TestSql
    {
        [GeneratedRegex(@"\[([^\[\]]+)\]")]
        private static partial Regex Bracketed();

        /// <summary>
        ///     Rewrites <c>[Name]</c> in <paramref name="sql"/> to the quoting of the provider
        ///     behind <paramref name="connection"/>.
        /// </summary>
        public static string Quote(DbConnection connection, string sql)
        {
            var formatter = CommandBuilderFactory.GetCommandBuilder(connection).DatabaseFormatter;
            return Bracketed().Replace(sql, match => formatter.FormatColumnName(match.Groups[1].Value));
        }

        /// <summary>
        ///     <see cref="Quote(DbConnection, string)"/> for the tests that have no connection of
        ///     their own, because they go through <c>IDatabase</c>: opens one from
        ///     <paramref name="fixture"/> only to find the provider's formatter.
        /// </summary>
        public static async Task<string> QuoteAsync(IDatabaseFixture fixture, string sql)
        {
            await using var connection = await fixture.OpenAsync();
            return Quote(connection, sql);
        }
    }
}
