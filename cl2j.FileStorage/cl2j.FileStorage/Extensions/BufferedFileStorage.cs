using System.Diagnostics;
using System.Text;
using cl2j.FileStorage.Core;

namespace cl2j.FileStorage.Extensions
{
    public class BufferedFileStorage : IDisposable
    {
        // Le minuteur etait `static`. Deux instances partageaient donc le champ : la seconde
        // orphelinait le minuteur de la premiere, et `Dispose` liberait celui de la derniere
        // creee — donc disposer une instance arretait silencieusement le depot periodique d une
        // autre, sans exception ni trace. Sans effet tant qu une application n a qu un seul
        // `LoggerProvider`, ce qui est le cas courant, mais rien ne l imposait.
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
        /// L horloge qui nomme les fichiers et decide de la bascule. Par defaut UTC, ce qui garde
        /// le comportement des appelants existants.
        ///
        /// <para>Elle existe parce que le nom et le contenu divergeaient : le journal horodatait
        /// ses lignes dans le fuseau configure par l application, pendant que le nom du fichier et
        /// la bascule etaient en UTC en dur. Un fichier nomme pour le 6 septembre s ouvrait donc le
        /// 5 a 19 h 59, et diagnostiquer une soiree demandait d ouvrir le fichier du lendemain.</para>
        ///
        /// <para>⚠️ Les trois usages — le nom, les deux tests de bascule et la marque du dernier
        /// ecrit — doivent partager la meme horloge. N en corriger qu un les fait diverger a
        /// nouveau.</para>
        /// </param>
        public BufferedFileStorage(IFileStorageProvider fileStorageProvider, string fileNamePattern, int maxSize, TimeSpan flushInterval, bool clearFile, Func<DateTime>? clock = null)
        {
            this.fileStorageProvider = fileStorageProvider;
            this.fileNamePattern = fileNamePattern;
            this.maxSize = maxSize;
            // Assignee avant ClearFileAsync : ce dernier nomme deja un fichier.
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