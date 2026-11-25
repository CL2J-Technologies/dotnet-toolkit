namespace cl2j.Tooling
{
    public static class DateUtils
    {
        public static bool InRange(this DateTimeOffset time, DateTimeOffset? start, DateTimeOffset? end)
        {
            if (start.HasValue && start.Value > time)
                return false;
            if (end.HasValue && end.Value <= time)
                return false;
            return true;
        }
    }
}
