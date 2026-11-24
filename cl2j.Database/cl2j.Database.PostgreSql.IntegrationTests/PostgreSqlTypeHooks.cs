namespace cl2j.Database.PostgreSql.IntegrationTests
{
    /// <summary>
    ///     What PostgreSQL stores or returns differently from SQL Server, for the hooks the type
    ///     matrix and the edge-value tests share (C# cannot inherit one override into two bases, so
    ///     both derived classes forward here).
    /// </summary>
    internal static class PostgreSqlTypeHooks
    {
        /// <summary>timestamp, timestamptz and time keep microseconds.</summary>
        public const int TemporalFractionalDigits = 6;

        /// <summary>
        ///     timestamptz holds an instant and no offset, so an offset comes back as UTC. Equality
        ///     of DateTimeOffset compares the instant either way; the offset is what changes.
        /// </summary>
        public static object ExpectedValue(object written)
            => written is DateTimeOffset moment ? moment.ToUniversalTime() : written;

        /// <summary>The <c>DATA_TYPE</c> PostgreSQL's catalog reports for each mapped CLR type.</summary>
        public static string ExpectedColumnType(Type clrType)
        {
            if (clrType.IsEnum || clrType == typeof(int))
                return "integer";
            if (clrType == typeof(bool))
                return "boolean";
            if (clrType == typeof(byte) || clrType == typeof(short))
                return "smallint";
            if (clrType == typeof(long))
                return "bigint";
            if (clrType == typeof(float))
                return "real";
            if (clrType == typeof(double))
                return "double precision";
            if (clrType == typeof(decimal))
                return "numeric";
            if (clrType == typeof(Guid))
                return "uuid";
            if (clrType == typeof(string))
                return "character varying"; //the matrix string columns declare a length; unbounded text is covered in PostgreSqlSpecificTests
            if (clrType == typeof(byte[]))
                return "bytea";
            if (clrType == typeof(DateTime))
                return "timestamp without time zone";
            if (clrType == typeof(DateTimeOffset))
                return "timestamp with time zone";
            if (clrType == typeof(DateOnly))
                return "date";
            if (clrType == typeof(TimeOnly))
                return "time without time zone";
            if (clrType == typeof(TimeSpan))
                return "interval";
            if (clrType == typeof(char))
                return "character";
            throw new NotSupportedException($"No expected column type for {clrType}.");
        }
    }
}
