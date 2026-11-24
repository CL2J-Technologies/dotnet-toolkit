namespace cl2j.Database.MySql.IntegrationTests
{
    /// <summary>
    ///     MySQL's answers to the hooks <c>TypeMatrixTestsBase</c> and <c>TypeEdgeValueTestsBase</c>
    ///     share. C# cannot inherit one override into two bases, so each derived class forwards here.
    /// </summary>
    internal static class MySqlTypeHooks
    {
        //datetime(6), datetime(6) and time(6): one microsecond, not the seventh digit of a tick.
        internal const int TemporalFractionalDigits = 6;

        //The column holds an instant: the offset does not come back.
        internal static object ExpectedValue(object written) => written switch
        {
            DateTimeOffset moment => moment.ToUniversalTime(),
            _ => written,
        };

        internal static string ExpectedColumnType(Type clrType)
        {
            if (clrType.IsEnum)
                return "int";
            if (clrType == typeof(bool) || clrType == typeof(byte))
                return "tinyint";
            if (clrType == typeof(short))
                return "smallint";
            if (clrType == typeof(int))
                return "int";
            if (clrType == typeof(long))
                return "bigint";
            //float is stored as double: see MySqlCommandBuilder.MapClrType.
            if (clrType == typeof(float) || clrType == typeof(double))
                return "double";
            if (clrType == typeof(decimal))
                return "decimal";
            if (clrType == typeof(Guid) || clrType == typeof(char))
                return "char";
            if (clrType == typeof(string))
                return "varchar";
            if (clrType == typeof(byte[]))
                return "longblob";
            if (clrType == typeof(DateTime) || clrType == typeof(DateTimeOffset))
                return "datetime";
            if (clrType == typeof(DateOnly))
                return "date";
            if (clrType == typeof(TimeOnly) || clrType == typeof(TimeSpan))
                return "time";
            throw new NotSupportedException($"No expected column type for {clrType}.");
        }
    }
}
