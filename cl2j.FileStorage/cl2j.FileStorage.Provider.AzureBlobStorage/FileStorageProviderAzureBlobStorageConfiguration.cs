using cl2j.FileStorage.Core;

namespace cl2j.FileStorage.Provider.AzureBlobStorage
{
    public class FileStorageProviderAzureBlobStorageConfiguration : FileStorageConfiguration
    {
        public string Container { get; set; } = null!;
        public string ConnectionString { get; set; } = null!;
    }
}