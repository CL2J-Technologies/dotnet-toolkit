using System.Diagnostics;
using cl2j.FileStorage.Core;
using cl2j.FileStorage.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace cl2j.Logging
{
    public class LoggerProvider : ILoggerProvider
    {
        public readonly MemoryLogger MemoryLogger = null!;
        private readonly LoggerOptions options;

        public LoggerProvider(IFileStorageFactory fileStorageFactory, IOptions<LoggerOptions> options, string fileStorageName = "Logger")
        {
            this.options = options.Value;

            if (this.options.MemoryLogs)
                MemoryLogger = new MemoryLogger();

            var fileStorage = fileStorageFactory.GetProvider(fileStorageName);
            if (fileStorage != null)
                BufferedFile = new BufferedFileStorage(fileStorage, this.options.MainFileNamePattern, this.options.MaxSize, this.options.FlushInterval, this.options.ClearOnStartup);
            else
                Debug.WriteLine($"LoggerProvider: FileStorage '{fileStorageName}' not found");
        }

        public BufferedFileStorage BufferedFile { get; } = null!;

        public bool WriteToConsole => options.IncludeConsole;

        public void ClearMemoryLogs()
        {
            MemoryLogger?.Clear();
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new Logger(this, categoryName, options.Filters, DateTimeProvider.Create(options.TimeZoneName));
        }

        public void Dispose()
        {
            BufferedFile?.Dispose();
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
                BufferedFile?.FlushAsync().Wait();
        }
    }
}