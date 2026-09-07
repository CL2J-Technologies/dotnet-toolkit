using System.Collections.Concurrent;
using cl2j.Tooling;
using cl2j.Tooling.Exceptions;
using Microsoft.Extensions.Logging;

namespace cl2j.DataStore
{
    public abstract class DataStoreDictionaryFactoryBase(ILogger logger) : IDataStoreWarmableSource
    {
        private readonly ConcurrentDictionary<string, object> dict = new();

        /// <summary>
        ///     The registered stores that hold a cache. The registry already knows all of them;
        ///     this is the only thing that was missing to warm them without naming each one again
        ///     with its type arguments. See issue #34.
        /// </summary>
        public IReadOnlyCollection<IDataStoreWarmable> GetWarmable() => [.. dict.Values.OfType<IDataStoreWarmable>()];

        protected void AddDataStore(string name, object dataStore)
        {
            if (!dict.TryAdd(name, dataStore))
                throw new ConflictException($"Data store '{name}' already added.");
            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation($"Added DataStore {name} [{dataStore.GetType().GetPrettyName()}]");
        }

        protected T Get<T>(string name) where T : class
        {
            if (dict.TryGetValue(name, out var dataStore))
            {
                var casted = dataStore as T ?? throw new ConflictException($"Data store '{name}' type '{dataStore.GetType().FullName}' doesn't match requested type '{typeof(T).FullName}'.");
                return casted;
            }

            throw new NotFoundException($"Data store '{name}' doesn't exists.");
        }
    }
}
