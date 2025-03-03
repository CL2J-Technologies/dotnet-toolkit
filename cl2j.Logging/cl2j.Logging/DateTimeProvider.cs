namespace cl2j.Logging
{
    internal sealed class DateTimeProvider(TimeZoneInfo? tzi) : IDateTimeProvider
    {
        public static DateTimeProvider Create(string timeZone)
        {
            ArgumentNullException.ThrowIfNull(timeZone);

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