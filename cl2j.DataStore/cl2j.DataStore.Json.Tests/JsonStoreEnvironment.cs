using cl2j.DataStore.Dictionary;
using cl2j.DataStore.List;
using cl2j.FileStorage.Core;
using cl2j.FileStorage.Provider.Disk;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace cl2j.DataStore.Json.Tests
{
    public sealed class Person
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
    }

    /// <summary>
    ///     A disk provider over a unique temporary directory, and a service provider that hands it
    ///     out under <see cref="StorageName"/>. The real <c>FileStorageFactory</c> is not used: it
    ///     keeps its providers in a static dictionary, which would let tests see each other.
    /// </summary>
    public sealed class JsonStoreEnvironment : IDisposable
    {
        public const string StorageName = "test-storage";

        private readonly string root;

        public JsonStoreEnvironment()
        {
            root = Path.Combine(Path.GetTempPath(), "cl2j-datastore-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Disk:Path"] = root })
                .Build();

            Provider = new FileStorageProviderDisk();
            Provider.Initialize("Disk", configuration.GetSection("Disk"));

            ListFactory = new DataStoreListFactory(NullLogger<DataStoreListFactory>.Instance);
            DictionaryFactory = new DataStoreDictionaryFactory(NullLogger<DataStoreDictionaryFactory>.Instance);

            var services = new ServiceCollection();
            services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
            services.AddSingleton<IFileStorageFactory>(new SingleProviderFactory(Provider));
            services.AddSingleton<IDataStoreListFactory>(ListFactory);
            services.AddSingleton<IDataStoreDictionaryFactory>(DictionaryFactory);
            Services = services.BuildServiceProvider();
        }

        public FileStorageProviderDisk Provider { get; }

        public DataStoreListFactory ListFactory { get; }

        public DataStoreDictionaryFactory DictionaryFactory { get; }

        public IServiceProvider Services { get; }

        public void Dispose()
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }

        private sealed class SingleProviderFactory(IFileStorageProvider provider) : IFileStorageFactory
        {
            public void Register<T>(string type) where T : IFileStorageProvider, new() => throw new NotSupportedException();

            public IFileStorageProvider GetProvider(string name) => name == StorageName ? provider : throw new KeyNotFoundException(name);
        }
    }
}
