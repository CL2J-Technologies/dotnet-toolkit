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

        // Une seule instance, partagee par le nom du fichier et par les lignes. Deux instances
        // donneraient le meme resultat, mais rien ne garantirait qu elles le donnent toujours :
        // c est justement l ecart entre deux horloges qui a produit le defaut.
        private readonly IDateTimeProvider dateTimeProvider;

        public LoggerProvider(IFileStorageFactory fileStorageFactory, IOptions<LoggerOptions> options, string fileStorageName = "Logger")
        {
            this.options = options.Value;
            dateTimeProvider = DateTimeProvider.Create(this.options.TimeZoneName);

            if (this.options.MemoryLogs)
                MemoryLogger = new MemoryLogger();

            var fileStorage = fileStorageFactory.GetProvider(fileStorageName);
            if (fileStorage != null)
                BufferedFile = new BufferedFileStorage(fileStorage, this.options.MainFileNamePattern, this.options.MaxSize, this.options.FlushInterval, this.options.ClearOnStartup, () => dateTimeProvider.Now().DateTime);
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
            return new Logger(this, categoryName, dateTimeProvider);
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