using System.Diagnostics;
using System.Text;
using cl2j.FileStorage.Core;

namespace cl2j.FileStorage.Extensions
{
    public class BufferedFileStorage : IDisposable
    {
        // The timer used to be `static`, so two instances shared the field: the second orphaned
        // the timer of the first, and `Dispose` released the one belonging to the last instance
        // created — so disposing one instance silently stopped another instance periodic flush,
        // with no exception and no trace. Harmless as long as an application has a single
        // `LoggerProvider`, which is the common case, but nothing enforced it.
        private readonly Timer timer;
        private readonly StringBuilder buffer = new();
        private readonly string fileNamePattern;
        private readonly IFileStorageProvider fileStorageProvider;
        private readonly int maxSize;

        private readonly SemaphoreSlim semaphoreSlim = new(1);
        private readonly Func<DateTime> clock;
        private int currentTotalSize;
        private DateTime lastWrite;

        /// <param name="clock">
        /// The clock that names the files and decides when to roll over. UTC by default, which
        /// preserves the behaviour of existing callers.
        ///
        /// <para>It exists because the name and the content disagreed: the log stamped its lines
        /// in the time zone the application configured, while the file name and the rollover were
        /// hard-coded to UTC. A file named for September 6th therefore opened on the 5th at 19:59,
        /// and diagnosing an evening meant opening the next day file.</para>
        ///
        /// <para>⚠️ All three uses — the name, the two rollover checks and the last-written mark —
        /// must share the same clock. Fixing only one of them makes them diverge again.</para>
        /// </param>
        public BufferedFileStorage(IFileStorageProvider fileStorageProvider, string fileNamePattern, int maxSize, TimeSpan flushInterval, bool clearFile, Func<DateTime>? clock = null)
        {
            this.fileStorageProvider = fileStorageProvider;
            this.fileNamePattern = fileNamePattern;
            this.maxSize = maxSize;
            // Assigned before ClearFileAsync: that call already names a file.
            this.clock = clock ?? (() => DateTime.UtcNow);

            lastWrite = DateTime.MinValue;

            if (clearFile)
                ClearFileAsync().Wait();

            timer = new Timer(RefreshAsync, null, TimeSpan.Zero, flushInterval);
        }

        private async Task ClearFileAsync()
        {
            await NextFileNameAsync();

            if (await fileStorageProvider.ExistsAsync(CurrentFileName))
                await fileStorageProvider.DeleteAsync(CurrentFileName);
        }

        public string CurrentFileName { get; private set; } = null!;

        public async Task AppendAsync(string message)
        {
            await semaphoreSlim.WaitAsync();
            try
            {
                buffer.Append(message);
            }
            finally
            {
                semaphoreSlim.Release();
            }
        }

        public void Dispose()
        {
            FlushAsync().Wait();

            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public async Task FlushAsync()
        {
            if (buffer.Length == 0)
                return;

            if (clock().Date != lastWrite.Date || (maxSize > 0 && currentTotalSize > maxSize))
            {
                await semaphoreSlim.WaitAsync();
                try
                {
                    //Ensure that the filename was not obtained since the lock
                    if (clock().Date != lastWrite.Date || (maxSize > 0 && currentTotalSize > maxSize))
                        await NextFileNameAsync();
                }
                finally
                {
                    semaphoreSlim.Release();
                }
            }

            string bufferValue;
            await semaphoreSlim.WaitAsync();
            try
            {
                //Ensure that the buffer was not flushed since the lock was obtained
                if (buffer.Length == 0)
                    return;

                currentTotalSize += buffer.Length;

                bufferValue = buffer.ToString();
                buffer.Length = 0; // Clear buffer

                lastWrite = clock();
            }
            finally
            {
                semaphoreSlim.Release();
            }

            Exception? exception = null;
            var nb = 0;
            while (nb < 2)
            {
                try
                {
                    await fileStorageProvider.AppendTextAsync(CurrentFileName, bufferValue);
                    break;
                }
                catch (Exception e)
                {
                    exception = e;
                    ++nb;
                }
            }

            if (exception != null)
                Console.WriteLine($"BufferedFileStorage: Exception occured writing to file '{CurrentFileName}' : {exception.Message}");
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                semaphoreSlim.Dispose();
                timer.Dispose();
            }
        }

        private async Task NextFileNameAsync()
        {
            var lastNumber = 1;
            while (true)
            {
                var fileName = string.Format(fileNamePattern, clock(), lastNumber).ToLowerInvariant();
                var fileInfo = await fileStorageProvider.GetInfoAsync(fileName);

                var size = fileInfo?.Size ?? 0;
                if (fileInfo == null || size < maxSize)
                {
                    CurrentFileName = fileName;
                    currentTotalSize = (int)size;

                    Debug.WriteLine($"BufferedFileStorage: New fileName '{CurrentFileName}'");
                    return;
                }

                ++lastNumber;
            }
        }

        public async void RefreshAsync(object? state)
        {
            await FlushAsync();
        }
    }
}