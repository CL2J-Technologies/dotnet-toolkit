using cl2j.FileStorage.Core;

namespace cl2j.FileStorage.Provider.Disk
{
    public class FileStorageProviderDiskConfiguration : FileStorageConfiguration
    {
        public string Path { get; set; } = null!;
    }
}