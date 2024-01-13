using cl2j.FileStorage.Core;
using System.Diagnostics;
using System.Text;

namespace cl2j.FileStorage.Extensions
{
    public class BufferedFileStorage : IDisposable
    {
        private static Timer timer = null!;
        private readonly StringBuilder buffer = new();
        private readonly string fileNamePattern;
        private readonly IFileStorageProvider fileStorageProvider;
        private readonly int maxSize;

        private readonly SemaphoreSlim semaphoreSlim = new(1);
        private int currentTotalSize;
        private DateTime lastWrite;

        public BufferedFileStorage(IFileStorageProvider fileStorageProvider, string fileNamePattern, int maxSize, TimeSpan flushInterval, bool clearFile)
        {
            this.fileStorageProvider = fileStorageProvider;
            this.fileNamePattern = fileNamePattern;
            this.maxSize = maxSize;

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

            if (DateTime.UtcNow.Date != lastWrite.Date || (maxSize > 0 && currentTotalSize > maxSize))
            {
                await semaphoreSlim.WaitAsync();
                try
                {
                    //Ensure that the filename was not obtained since the lock
                    if (DateTime.UtcNow.Date != lastWrite.Date || (maxSize > 0 && currentTotalSize > maxSize))
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

                lastWrite = DateTime.UtcNow;
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
                var fileName = string.Format(fileNamePattern, DateTime.UtcNow, lastNumber).ToLower();
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