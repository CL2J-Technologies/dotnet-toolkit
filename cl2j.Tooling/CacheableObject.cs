using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace cl2j.Tooling
{
    public abstract class CacheableObject<T>
    {
        private readonly CacheLoader cacheLoader;
        private IEnumerable<T> cache = new List<T>();

        public CacheableObject(string name, TimeSpan refreshInterval, ILogger logger)
        {
            cacheLoader = new CacheLoader(name, refreshInterval, async () =>
            {
                try
                {
                    var sw = Stopwatch.StartNew();
                    cache = await LoadCache();
                    logger.LogDebug($"{name} --> {cache.Count()} in {sw.ElapsedMilliseconds}ms");
                }
                catch (Exception ex)
                {
                    logger.LogCritical(ex, $"{name} --> Unable to load cache");
                }
            }, logger);
        }

        protected abstract Task<IEnumerable<T>> LoadCache();

        protected async Task<IEnumerable<T>> GetCache()
        {
            await cacheLoader.WaitAsync();
            return cache;
        }
    }
}
