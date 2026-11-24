using cl2j.Database.DataAnnotations;
using Xunit;

namespace cl2j.Database.IntegrationTests
{
    /// <summary>
    ///     SQL Server's run of the shared <see cref="TypeEdgeValueTestsBase"/>, which needs no
    ///     override (every default is SQL Server's own value), plus the cases that stay here
    ///     because they are about SQL Server's code-page behaviour: a <c>char</c> or
    ///     <c>varchar</c> column holds what the database collation's code page holds, and
    ///     <c>nchar</c> / <c>nvarchar</c> are the way out. Those type names are SQL Server's, and a
    ///     server that stores Unicode in its plain text columns has nothing to substitute.
    /// </summary>
    [Collection(SqlServerCollection.Name)]
    public class TypeEdgeValueTests(SqlServerFixture fixture) : TypeEdgeValueTestsBase(fixture)
    {
        [Fact]
        public async Task A_char_the_column_collation_has_survives()
        {
            //char maps to char(1), which is not Unicode: it holds what the database collation's
            //code page holds. e-acute is in Windows-1252.
            var (inserted, bulk) = await BothWays(new EdgeUnicodeRow { Letter = 'é', Unicode = 'é' });

            foreach (var read in new[] { inserted, bulk })
            {
                Assert.Equal('é', read.Letter);
                Assert.Equal('é', read.Unicode);
            }
        }

        [Fact]
        public async Task A_char_outside_the_collation_is_substituted_unless_the_column_is_declared_nchar()
        {
            //Pinned, not endorsed: a Greek letter is not in Windows-1252, and SQL Server stores a
            //lossy substitute ('?' or a best-fit letter such as 'O') with no error. TypeName = "nchar(1)"
            //is the way out.
            var (inserted, bulk) = await BothWays(new EdgeUnicodeRow { Letter = 'Ω', Unicode = 'Ω' });

            foreach (var read in new[] { inserted, bulk })
            {
                Assert.NotEqual('Ω', read.Letter);
                Assert.Equal('Ω', read.Unicode);
            }
        }

        [Fact]
        public async Task A_string_outside_the_collation_becomes_question_marks_unless_the_column_is_declared_nvarchar()
        {
            var (inserted, bulk) = await BothWays(new EdgeUnicodeRow { Text = "café Привет", UnicodeText = "café Привет" });

            foreach (var read in new[] { inserted, bulk })
            {
                Assert.Equal("café ??????", read.Text);
                Assert.Equal("café Привет", read.UnicodeText);
            }
        }
    }

    /// <summary>
    ///     The columns of the SQL Server code-page cases: a plain and a national version of a
    ///     character and of a string, side by side, so that each test reads one row back and
    ///     compares the two.
    /// </summary>
    [Table("EdgeUnicodeValues")]
    public sealed class EdgeUnicodeRow
    {
        [Column(Name = "Id", Key = KeyType.Key)]
        public int Id { get; set; }

        [Column(Name = "Letter")]
        public char Letter { get; set; } = 'a';

        [Column(Name = "Unicode", TypeName = "nchar(1)")]
        public char Unicode { get; set; } = 'a';

        [Column(Name = "Text", Length = 100)]
        public string? Text { get; set; }

        [Column(Name = "UnicodeText", TypeName = "nvarchar(100)")]
        public string? UnicodeText { get; set; }
    }
}
