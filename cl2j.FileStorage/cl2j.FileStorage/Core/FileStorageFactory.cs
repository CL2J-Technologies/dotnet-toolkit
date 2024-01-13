using cl2j.Tooling.Exceptions;
using Microsoft.Extensions.Configuration;

namespace cl2j.FileStorage.Core
{
    public class FileStorageFactory : IFileStorageFactory
    {
        private static readonly object Lock = new();
        private readonly List<string> registrations = new();
        private readonly IConfigurationRoot configuration;
        private static readonly Dictionary<string, IFileStorageProvider> storageProviderInstances = new();

        public FileStorageFactory(IConfigurationRoot configuration)
        {
            this.configuration = configuration;
        }

        public void Register<T>(string type) where T : IFileStorageProvider, new()
        {
            var section = configuration.GetSection("cl2j:FileStorage:Storages");

            var childrenConfigs = section.GetChildren();
            foreach (var childConfig in childrenConfigs)
            {
                var providerType = childConfig["Type"];
                if (providerType == type)
                {
                    var providerName = childConfig.Key;
                    if (!storageProviderInstances.ContainsKey(providerName))
                    {
                        var provider = new T();
                        provider.Initialize(providerName, childConfig);

                        storageProviderInstances.Add(providerName, provider);

                        registrations.Add($"'{providerName}' with provider '{typeof(T).Name}'");
                    }
                }
            }
        }

        public IList<string> Registrations => registrations;

        public IFileStorageProvider GetProvider(string name)
        {
            //First search into the cached fileSystems
            if (storageProviderInstances.TryGetValue(name, out var fileStorageInstance))
                return fileStorageInstance;

            //Create the fileSystem
            lock (Lock)
            {
                if (storageProviderInstances.TryGetValue(name, out fileStorageInstance))
                    return fileStorageInstance;
            }

            throw new NotFoundException($"FileStorageProvider '{name}' not found");
        }
    }
}