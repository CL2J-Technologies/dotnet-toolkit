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

        /// <summary>The first day of MySQL's <c>datetime</c> and <c>date</c> range.</summary>
        internal static readonly DateTime FirstDay = new(1000, 1, 1);

        //MySQL's datetime and date start in the year 1000, not 0001.
        internal static object ValueToWrite(object value) => value switch
        {
            DateTime stamp when stamp < FirstDay => DateTime.SpecifyKind(FirstDay, stamp.Kind),
            DateOnly day when day < DateOnly.FromDateTime(FirstDay) => DateOnly.FromDateTime(FirstDay),
            DateTimeOffset moment when moment.UtcDateTime < FirstDay => new DateTimeOffset(FirstDay, TimeSpan.Zero),
            _ => value,
        };

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
            if (clrType == typeof(float))
                return "float";
            if (clrType == typeof(double))
                return "double";
            if (clrType == typeof(decimal))
                return "decimal";
            if (clrType == typeof(Guid) || clrType == typeof(char))
                return "char";
            if (clrType == typeof(string))
                return "longtext";
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
