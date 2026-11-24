using System.Data;
using cl2j.Database.DataAnnotations;
using cl2j.Database.Descriptors;

namespace cl2j.Database.Helpers
{
    /// <summary>
    ///     Decides whether a parameter needs a <see cref="DbType"/> of our own, rather than the one
    ///     ADO.NET infers from the CLR type.
    ///
    ///     <para>
    ///     There are two such cases; the second, <see cref="DateTime"/> bound as <c>datetime2</c>, is
    ///     explained where it is decided. First: a .NET <c>string</c> infers as <c>nvarchar</c>. Compared against
    ///     a <c>varchar</c> column, SQL Server's data-type precedence puts <c>nvarchar</c> higher
    ///     and converts <b>the column</b> rather than the parameter — and a converted column cannot
    ///     be seeked. The index is still there; the query simply stops using it and scans instead.
    ///     Measured on a million-row table, reading one row by its primary key: 34,235 logical
    ///     reads against 3.
    ///     </para>
    ///
    ///     <para>
    ///     Nothing fails. No exception, no plan warning, and the answer is byte-for-byte correct.
    ///     The only symptom is an application that gets slower as its tables grow, which reads as
    ///     "we need more hardware" rather than as a defect.
    ///     </para>
    /// </summary>
    internal static class ParameterTyping
    {
        /// <summary>
        ///     Returns <see cref="DbType.AnsiString"/> for a string bound to a key column,
        ///     <see cref="DbType.DateTime2"/> for a <see cref="DateTime"/>, and <c>null</c> —
        ///     meaning leave ADO.NET's inference alone — for everything else.
        ///
        ///     <para>
        ///     <b>The narrowness is the point, and it is not a matter of taste.</b> Free-text
        ///     columns — a biography, a city, a title — are genuinely <c>nvarchar</c> and must stay
        ///     that way. Binding one as <see cref="DbType.AnsiString"/> silently drops every
        ///     non-ASCII character on write: an accent, an emoji, a name in Cyrillic, gone, with no
        ///     error. That is data loss, which is strictly worse than a slow query.
        ///     </para>
        ///
        ///     <para>
        ///     A key is an identifier or a natural code, not prose, so the conversion is safe there
        ///     and that is where the cost lands anyway — the whole of the "fetch the row with id X"
        ///     surface. Unicode stays the default everywhere else, which means forgetting to mark a
        ///     column costs speed, never correctness.
        ///     </para>
        /// </summary>
        internal static DbType? DbTypeFor(ColumnDescriptor? column, object? value)
        {
            //A DateTime infers as datetime: 1753 onwards, rounded to 1/300 of a second. The column
            //this library creates for it is datetime2, which holds year 1 and 100 ns, so the
            //parameter has to be as wide as the column or DateTime.MinValue throws and a value with
            //a tick in it is rounded on the way in. A column declared another type by TypeName is
            //left alone.
            if (value is DateTime)
                return DeclaredOtherThanDateTime2(column?.ColumnAtribute.TypeName) ? null : DbType.DateTime2;

            if (value is not string)
                return null;

            //No column means nothing tells us the value is an identifier rather than prose.
            if (column is null || column.ColumnAtribute.Key == KeyType.None)
                return null;

            //The same binding runs on INSERT and UPDATE, not only on reads, so a key column that
            //is genuinely Unicode would lose its non-ASCII characters on write. TypeName is where
            //a schema says so, and it wins over the default.
            if (DeclaredUnicode(column.ColumnAtribute.TypeName))
                return null;

            return DbType.AnsiString;
        }

        private static bool DeclaredOtherThanDateTime2(string? typeName)
            => !string.IsNullOrWhiteSpace(typeName) && !typeName.TrimStart().StartsWith("datetime2", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        ///     True when a declared column type is one of the Unicode ones. Matches on the prefix
        ///     so that <c>nvarchar</c>, <c>nvarchar(100)</c>, <c>nchar(2)</c> and <c>ntext</c> all
        ///     read the same. Anything the schema does not declare falls through to the default.
        /// </summary>
        private static bool DeclaredUnicode(string? typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                return false;

            var trimmed = typeName.TrimStart();

            return trimmed.StartsWith("nvarchar", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("nchar", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("ntext", StringComparison.OrdinalIgnoreCase);
        }
    }
}
