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
                    await fileStorageProvider.DeleteAsync(Path.Combine(path, file));
                return files.Count();
            }
            catch
            {
                return 0;
            }
        }

        public static async Task<bool> Copy(this IFileStorageProvider fileStorageProvider, string sourcePath, string destinationPath)
        {
            try
            {
                var memoryStream = new MemoryStream();
                if (await fileStorageProvider.ReadAsync(sourcePath, memoryStream))
                {
                    memoryStream.Seek(0, SeekOrigin.Begin);
                    await fileStorageProvider.WriteAsync(destinationPath, memoryStream);
                }
            }
            catch
            {
            }

            return false;
        }
    }
}
