using System.Data;
using System.Data.Common;
using cl2j.Database.Descriptors;

namespace cl2j.Database.Helpers
{
    /// <summary>
    ///     Builds every parameter the library sends, and declares it by its type rather than by its
    ///     value.
    ///
    ///     <para>
    ///     Left to itself, SqlClient sizes a parameter from the value it holds: <c>nvarchar(3)</c>
    ///     for "abc", <c>nvarchar(10)</c> for a ten-character string, <c>decimal(3,1)</c> for 45.5,
    ///     <c>decimal(14,12)</c> for 45.123456789012, and <c>nvarchar(4000)</c> for a null whatever
    ///     the column. The declarations are part of the batch text SQL Server caches plans by, so
    ///     every new length, scale or null is a new text: one more compilation, one more plan in
    ///     the cache. Measured on a recommendation query: 79 compilations for 206 executions, about
    ///     72 ms of CPU each time, and 25,000 cached plans after two passes over 400 users.
    ///     </para>
    ///
    ///     <para>
    ///     Nothing here may change a value. A declaration smaller than the value is not an error to
    ///     SqlClient: it sends the first <c>Size</c> characters, or rounds to the declared scale,
    ///     and says nothing. Each rule therefore widens past what it fixes, or steps aside.
    ///     </para>
    /// </summary>
    internal static class ParameterDeclaration
    {
        /// <summary>The longest <c>nvarchar</c> that is not <c>max</c>.</summary>
        internal const int UnicodeSize = 4000;

        /// <summary>The longest <c>varchar</c> that is not <c>max</c>.</summary>
        internal const int AnsiSize = 8000;

        /// <summary><c>decimal(38,18)</c>: twenty digits before the point, eighteen after.</summary>
        internal const byte DecimalPrecision = 38;

        internal const byte DecimalScale = 18;

        //The smallest magnitude decimal(38,18) cannot hold: 10^20.
        private const decimal DecimalLimit = 100_000_000_000_000_000_000m;

        /// <summary>
        ///     Creates the parameter, gives it its value, type and declaration, and adds it to the
        ///     command.
        /// </summary>
        /// <param name="column">The property the value came from, when there is one. Its type is
        ///     what declares a null.</param>
        /// <param name="dbType">A type the caller insists on. Without one, <see cref="ParameterTyping"/>
        ///     decides, and a null takes the type of its property.</param>
        internal static DbParameter Bind(DbCommand command, string name, ColumnDescriptor? column, object? value, DbType? dbType = null)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;

            var declaredType = column is null ? null : Nullable.GetUnderlyingType(column.Property.PropertyType) ?? column.Property.PropertyType;

            var type = dbType ?? ParameterTyping.DbTypeFor(column, value) ?? (value is null ? NullType(command, column, declaredType) : null);
            if (type is not null)
                parameter.DbType = type.Value;

            Declare(parameter, value, declaredType);

            command.Parameters.Add(parameter);
            return parameter;
        }

        /// <summary>
        ///     The type a null is declared with: the one a value of the same property would get, so
        ///     that a query run with and without a value is one text rather than two. Null when the
        ///     property says nothing useful, which leaves the provider's default in place.
        /// </summary>
        private static DbType? NullType(DbCommand command, ColumnDescriptor? column, Type? declaredType)
        {
            if (declaredType is null)
                return null;

            //The key rule reads the value, and a null has none: ask it about an empty string of the
            //same column, which is what a value of that property would be.
            if (declaredType == typeof(string))
                return ParameterTyping.DbTypeFor(column, string.Empty) ?? DbType.String;

            if (declaredType == typeof(byte[]))
                return DbType.Binary;

            if (!declaredType.IsValueType)
                return null;

            //Inferred by the provider itself from a default value of the type, rather than from a
            //table of our own: the declaration a value gets is exactly the provider's inference, and
            //a table could only ever drift from it.
            var probe = command.CreateParameter();
            probe.Value = Activator.CreateInstance(declaredType)!;
            return probe.DbType;
        }

        private static void Declare(DbParameter parameter, object? value, Type? declaredType)
        {
            if (value is string || (value is null && declaredType == typeof(string)))
            {
                var size = parameter.DbType == DbType.AnsiString ? AnsiSize : UnicodeSize;
                parameter.Size = value is string text && text.Length > size ? -1 : size;
            }
            else if (value is decimal number ? Fits(number) : value is null && declaredType == typeof(decimal))
            {
                parameter.Precision = DecimalPrecision;
                parameter.Scale = DecimalScale;
            }
        }

        //decimal(38,18) would round the eighteenth decimal and overflow at 10^20. Such a value keeps
        //the provider's own declaration: a plan of its own is a fair price for keeping every digit.
        private static bool Fits(decimal number) => number.Scale <= DecimalScale && Math.Abs(number) < DecimalLimit;
    }
}
