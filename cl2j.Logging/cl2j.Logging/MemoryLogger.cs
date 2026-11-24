using Microsoft.Extensions.Logging;

namespace cl2j.Logging
{
    public class MemoryLogger(int maxLines = 1000)
    {
        private readonly int maxLines = ValidateMaxLines(maxLines);
        private readonly List<LogData> list = new(maxLines);

        public IList<LogData> GetLogs()
        {
            return list;
        }

        internal void Log(DateTimeOffset dateTime, LogLevel logLevel, Exception? ex, string text)
        {
            if (list.Count >= maxLines)
                list.RemoveAt(0);

            list.Add(new LogData
            {
                Stamp = dateTime,
                Level = logLevel,
                Text = text,
                Exception = ex
            });
        }

        internal void Clear()
        {
            list.Clear();
        }

        private static int ValidateMaxLines(int maxLines)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(maxLines, 1);
            return maxLines;
        }
    }
}