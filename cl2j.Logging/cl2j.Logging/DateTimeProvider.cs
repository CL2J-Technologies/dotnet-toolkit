namespace cl2j.Logging
{
    internal sealed class DateTimeProvider(TimeZoneInfo? tzi) : IDateTimeProvider
    {
        public static DateTimeProvider Create(string? timeZone)
        {
            // Un nom absent se comporte comme un nom inconnu : repli sur UTC avec un avertissement.
            // TimeZoneName n a pas de valeur par defaut dans LoggerOptions, et lever ici ferait
            // tomber le demarrage d une application qui ne l a jamais configuree — alors que le
            // repli existait deja pour un nom errone.
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