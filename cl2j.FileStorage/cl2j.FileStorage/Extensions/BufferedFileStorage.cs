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

        // Held for the whole of a flush, write included. `semaphoreSlim` guards the buffer and is
        // released before the write, so without this a second flush that started during the write
        // found an empty buffer and returned at once. When that second flush was the one in
        // `Dispose`, the instance reported itself disposed while the timer was still writing its
        // last lines — lost if the process was on its way out.
        private readonly SemaphoreSlim flushLock = new(1, 1);
        private int disposed;
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

            // First tick after one interval: the buffer is empty at construction, and a tick due
            // at once only raced whatever the caller did next.
            timer = new Timer(RefreshAsync, null, flushInterval, flushInterval);
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
            if (Interlocked.Exchange(ref disposed, 1) == 1)
                return;

            // Stop the ticks first, then flush: the flush waits for one the timer already started.
            timer.Dispose();
            FlushAsync().Wait();

            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public async Task FlushAsync()
        {
            await flushLock.WaitAsync();
            try
            {
                await FlushBufferAsync();
            }
            finally
            {
                flushLock.Release();
            }
        }

        private async Task FlushBufferAsync()
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
                timer.Dispose();
                semaphoreSlim.Dispose();
                flushLock.Dispose();
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
            // async void: anything that escapes here terminates the process rather than failing a
            // call, so nothing may escape. A tick queued before `Dispose` stopped the timer can
            // still arrive afterwards, and finds the semaphores disposed.
            if (Volatile.Read(ref disposed) == 1)
                return;

            try
            {
                await FlushAsync();
            }
            catch (ObjectDisposedException)
            {
                // Disposed while this tick was queued; `Dispose` has already flushed.
            }
            catch (Exception e)
            {
                Console.WriteLine($"BufferedFileStorage: Exception occured flushing to file '{CurrentFileName}' : {e.Message}");
            }
        }
    }
}