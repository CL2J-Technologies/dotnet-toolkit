using cl2j.FileStorage.Core;

namespace cl2j.FileStorage.Extensions
{
    public static class FileProviderExtensions
    {
        public static async Task<int> ClearDirectoryAsync(this IFileStorageProvider fileStorageProvider, string path)
        {
            try
            {
                var files = await fileStorageProvider.ListFilesAsync(path);
                foreach (var file in files)
                    await fileStorageProvider.DeleteAsync(file);
                return files.Count();
            }
            catch
            {
                return 0;
            }
        }
    }
}