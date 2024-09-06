using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace cl2j.Tooling
{
    public abstract class CacheableObject<T>
    {
        private readonly string name;
        private readonly ILogger logger;

        private readonly CacheLoader cacheLoader;
        private List<T> cache = new();

        public CacheableObject(string name, TimeSpan refreshInterval, ILogger logger)
        {
            cacheLoader = new CacheLoader(name, refreshInterval, async () => { await RefreshCache(); }, logger);
            this.name = name;
            this.logger = logger;
        }

        protected abstract Task<List<T>> LoadCache();

        protected async Task RefreshCache()
        {
            try
            {
                var sw = Stopwatch.StartNew();
                cache = await LoadCache();
                logger.LogDebug($"{name} --> {cache.Count} in {sw.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, $"{name} --> Unable to load cache");
            }
        }

        protected async Task<List<T>> GetCache()
        {
            await cacheLoader.WaitAsync();
            return cache;
        }
    }
}
