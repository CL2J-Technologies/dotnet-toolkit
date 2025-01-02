using System.Drawing;
using cl2j.FileStorage.Core;

namespace cl2j.Image
{
    public static class MediaHelpers
    {
        public static async Task UploadMediaAsync(IFileStorageProvider fileStorageProvider, string fileName, byte[] bytes, int max = 1280)
        {
            var bytesReturned = ImageUtils.CleanImage(bytes, max);
            if (bytesReturned != null)
            {
                using var ms = new MemoryStream(bytesReturned);
                await fileStorageProvider.WriteAsync(fileName, ms, GetContentTypeFromFileName(fileName));
            }
        }

        public static async Task UploadMediaAsync(IFileStorageProvider fileStorageProvider, string fileName, Bitmap image, int max = 1280)
        {
            var imageModified = ImageUtils.CleanImage(image, max, out var _);
            if (imageModified != null)
            {
                var bytes = ImageSerialization.SaveJpegToBytes(image);
                using var ms = new MemoryStream(bytes);
                await fileStorageProvider.WriteAsync(fileName, ms, GetContentTypeFromFileName(fileName));
            }
        }

        public static string? GetContentTypeFromFileName(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            if (extension == ".jpg" || extension == ".jpeg")
                return "image/jpeg";
            return null;
        }
    }
}
