using cl2j.Database.CommandBuilders;
using Microsoft.Data.SqlClient;
using Xunit;

namespace cl2j.Database.Tests
{
    /// <summary>
    ///     The standing guard against the class of defect fixed in GHSA-8hpr-28mq-ffc8: a value
    ///     supplied by a caller must never end up as text inside a statement.
    ///
    ///     <para>
    ///     This is deliberately not a test of one method. It holds for every builder that takes
    ///     caller values, including ones nobody has written a specific test for yet, so a new one
    ///     that inlines its arguments fails here rather than in production.
    ///     </para>
    /// </summary>
    public class NoInlinedValuesTests
    {
        private static ICommandBuilder Builder()
        {
            cl2j.Database.SqlServer.SqlServer.Register();
            return CommandBuilderFactory.GetCommandBuilder(new SqlConnection());
        }

        /// <summary>
        ///     Values that would break out of a literal, or that a naive escape would mangle.
        /// </summary>
        public static TheoryData<string> HostileValues =>
        [
            "O'Brien",
            "a' OR 1=1--",
            "'; DROP TABLE [Customer]--",
            "\" OR \"\"=\"",
            "Renée 😀",
            "[Id]",
            "@key0",
            ""
        ];

        /// <summary>
        ///     The same list without the two entries that collide with the statement's own syntax.
        ///     <c>[Id]</c> is a column name and <c>@key0</c> is a placeholder, so both appear in a
        ///     correct statement for reasons that have nothing to do with the value — searching for
        ///     them would report a leak that is not one. They stay in <see cref="HostileValues"/>,
        ///     where the assertion is about quotes and round-tripping rather than absence.
        /// </summary>
        public static TheoryData<string> HostileValuesThatCannotCollideWithSyntax =>
        [
            "O'Brien",
            "a' OR 1=1--",
            "'; DROP TABLE [Customer]--",
            "\" OR \"\"=\"",
            "Renée 😀"
        ];

        [Theory]
        [MemberData(nameof(HostileValuesThatCannotCollideWithSyntax))]
        public void A_key_value_never_reaches_the_statement_text(string value)
        {
            var statement = Builder().GetQueryByKeysStatement(typeof(Customer), [value]);

            Assert.DoesNotContain(value, statement.Text, StringComparison.Ordinal);
        }

        [Theory]
        [MemberData(nameof(HostileValues))]
        public void A_key_value_arrives_intact_as_a_parameter(string value)
        {
            var statement = Builder().GetQueryByKeysStatement(typeof(Customer), [value]);

            var parameter = Assert.Single(statement.Parameters);
            Assert.Equal(value, parameter.Value);
        }

        [Theory]
        [MemberData(nameof(HostileValues))]
        public void A_key_value_leaves_no_quote_behind(string value)
        {
            var statement = Builder().GetQueryByKeysStatement(typeof(Customer), [value]);

            //A quote in the text is the shape the defect had: the value had escaped its literal.
            //These statements have no legitimate reason to contain one.
            Assert.DoesNotContain("'", statement.Text, StringComparison.Ordinal);
        }

        [Fact]
        public void Every_key_of_a_long_list_is_a_parameter_and_none_is_text()
        {
            var keys = Enumerable.Range(0, 50).Select(i => (object)$"O'Brien-{i}").ToArray();

            var statement = Builder().GetQueryByKeysStatement(typeof(Customer), keys);

            Assert.Equal(50, statement.Parameters.Count);
            Assert.DoesNotContain("'", statement.Text, StringComparison.Ordinal);
            Assert.Equal(keys, statement.Parameters.Select(p => p.Value));
        }

        [Theory]
        [InlineData(typeof(Customer))]
        [InlineData(typeof(TenantScopedRow))]
        [InlineData(typeof(Counter))]
        public void No_builder_writes_a_quote_of_its_own(Type type)
        {
            var builder = Builder();

            foreach (var statement in new[]
            {
                builder.GetInsertStatement(type),
                builder.GetUpdateStatement(type),
                builder.GetDeleteStatement(type),
                builder.GetQueryStatement(type),
                builder.GetQueryByKeyStatement(type),
                builder.GetTableExistsStatement(type),
            })
            {
                Assert.DoesNotContain("'", statement.Text, StringComparison.Ordinal);
            }
        }

        [Theory]
        [InlineData(typeof(Customer))]
        [InlineData(typeof(TenantScopedRow))]
        [InlineData(typeof(Counter))]
        public void The_builders_that_bind_values_use_placeholders(Type type)
        {
            //GetQueryStatement and GetTableExistsStatement are excluded on purpose: they take no
            //values, so having no placeholder is correct for them rather than suspicious.
            var builder = Builder();

            foreach (var statement in new[]
            {
                builder.GetInsertStatement(type),
                builder.GetUpdateStatement(type),
                builder.GetDeleteStatement(type),
                builder.GetQueryByKeyStatement(type),
            })
            {
                Assert.Contains("@", statement.Text, StringComparison.Ordinal);
            }
        }
    }
}
