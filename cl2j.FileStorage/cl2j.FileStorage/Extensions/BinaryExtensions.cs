using cl2j.FileStorage.Core;
using cl2j.Tooling;

namespace cl2j.FileStorage.Extensions
{
    public static class BinaryExtensions
    {
        public static async Task<Stream?> ReadObjectAsync(this IFileStorageProvider fileStorageProvider, string fileName)
        {
            var stream = new MemoryStream();
            if (await fileStorageProvider.ReadAsync(fileName, stream))
                return stream;
            return null;
        }
        public static async Task<byte[]?> ReadBytesAsync(this IFileStorageProvider fileStorageProvider, string fileName)
        {
            var stream = new MemoryStream();
            if (await fileStorageProvider.ReadAsync(fileName, stream))
                return StreamUtils.ToBytes(stream);

            return null;
        }

        public static async Task WriteBytesAsync(this IFileStorageProvider fileStorageProvider, string fileName, byte[] bytes, string? contentType = null)
        {
            using var stream = new MemoryStream(bytes);
            await fileStorageProvider.WriteAsync(fileName, stream, contentType);
        }
    }
}