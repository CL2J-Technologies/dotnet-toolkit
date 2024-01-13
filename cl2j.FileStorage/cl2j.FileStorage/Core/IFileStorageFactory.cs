namespace cl2j.FileStorage.Core
{
    public interface IFileStorageFactory
    {
        void Register<T>(string type) where T : IFileStorageProvider, new();

        IFileStorageProvider GetProvider(string name);
    }
}