using System.Data;
using cl2j.Database.CommandBuilders;
using cl2j.Database.Descriptors;
using cl2j.Database.Helpers;
using Microsoft.Data.SqlClient;
using Xunit;

namespace cl2j.Database.Tests
{
    /// <summary>
    ///     A .NET string binds as <c>nvarchar</c>. Compared against a <c>varchar</c> column, SQL
    ///     Server's data-type precedence converts the column rather than the parameter, and a
    ///     converted column cannot be seeked — the index is still there, the query stops using it.
    ///     Nothing fails and the answer stays correct, so the only symptom is an application that
    ///     gets slower as its tables grow. See issue #7.
    ///
    ///     <para>
    ///     The rule has to stay narrow. Binding every string as ANSI would silently drop every
    ///     non-ASCII character on write — an accent, an emoji, a name in Cyrillic, gone, with no
    ///     error. That is data loss, which is strictly worse than a slow query. So: keys only,
    ///     where the value is an identifier rather than free text.
    ///     </para>
    /// </summary>
    public class ParameterTypingTests
    {
        private static TableDescriptor Describe(Type type)
        {
            cl2j.Database.SqlServer.SqlServer.Register();
            var formatter = CommandBuilderFactory.GetCommandBuilder(new SqlConnection()).DatabaseFormatter;
            return TableDescriptorFactory.Create(type, formatter);
        }

        private static ColumnDescriptor Column(Type type, string name)
            => Describe(type).Columns.Single(c => c.Name == name);

        [Fact]
        public void A_string_key_binds_as_ansi()
        {
            var dbType = ParameterTyping.DbTypeFor(Column(typeof(Customer), "Id"), "cust-1");

            Assert.Equal(DbType.AnsiString, dbType);
        }

        [Fact]
        public void A_string_column_that_is_not_a_key_is_left_alone()
        {
            //The guard against data loss. Name is free text and genuinely nvarchar; forcing ANSI
            //here would drop the accent on write and never say so.
            var dbType = ParameterTyping.DbTypeFor(Column(typeof(Customer), "Name"), "Renée");

            Assert.Null(dbType);
        }

        [Fact]
        public void A_key_that_is_not_a_string_is_left_alone()
        {
            var dbType = ParameterTyping.DbTypeFor(Column(typeof(Counter), "Id"), 42);

            Assert.Null(dbType);
        }

        [Fact]
        public void A_key_the_schema_declares_as_unicode_is_left_alone()
        {
            //The same binding runs on INSERT and UPDATE, not only on reads. A key column that is
            //genuinely nvarchar would lose its non-ASCII characters on write, silently. TypeName is
            //where the schema says so, and it is already carried into the descriptor.
            var dbType = ParameterTyping.DbTypeFor(Column(typeof(UnicodeKeyedRow), "Slug"), "café");

            Assert.Null(dbType);
        }

        [Fact]
        public void Without_a_column_there_is_no_opinion()
        {
            //Nothing says the value is an identifier, so inference stays as ADO.NET left it.
            var dbType = ParameterTyping.DbTypeFor(null, "cust-1");

            Assert.Null(dbType);
        }
    }
}
