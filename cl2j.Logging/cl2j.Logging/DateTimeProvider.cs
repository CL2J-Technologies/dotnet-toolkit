namespace cl2j.Logging
{
    internal sealed class DateTimeProvider(TimeZoneInfo? tzi) : IDateTimeProvider
    {
        public static DateTimeProvider Create(string? timeZone)
        {
            // A missing name behaves like an unknown one: fall back to UTC with a warning.
            // TimeZoneName has no default in LoggerOptions, and throwing here would bring down the
            // startup of an application that never configured it — while the fallback already
            // existed for a wrong name.
            if (string.IsNullOrWhiteSpace(timeZone))
            {
                Console.WriteLine("DateTimeProvider: no time zone configured. Using UTC");
                return new DateTimeProvider(null);
            }

            TimeZoneInfo? tzi = null;
            try
            {
                tzi = TimeZoneInfo.FindSystemTimeZoneById(timeZone);
            }
            catch
            {
                Console.WriteLine($"DateTimeProvider time zone '{timeZone}' doesn't exists. Using UTC");
            }

            return new DateTimeProvider(tzi);
        }

        public DateTimeOffset Now()
        {
            var datetime = DateTime.UtcNow;
            if (tzi != null)
                return TimeZoneInfo.ConvertTimeFromUtc(datetime, tzi);
            return datetime;
        }
    }
}