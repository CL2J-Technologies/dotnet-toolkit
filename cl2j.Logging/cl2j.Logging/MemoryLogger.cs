using Microsoft.Extensions.Logging;

namespace cl2j.Logging
{
    public class MemoryLogger(int maxLines = 1000)
    {
        private readonly List<LogData> list = new(maxLines);

        public IList<LogData> GetLogs()
        {
            return list;
        }

        internal void Log(DateTimeOffset dateTime, LogLevel logLevel, Exception? ex, string text)
        {
            if (!EnsureCapacityDoNotOverflow())
                return;

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

        private bool EnsureCapacityDoNotOverflow()
        {
            if (list.Count >= maxLines)
            {
                try
                {
                    list.RemoveAt(0);
                }
                catch { }
            }

            return list.Count < maxLines;
        }
    }
}