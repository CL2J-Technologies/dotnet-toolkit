using ImageRgba32 = SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>;
using cl2j.FileStorage.Core;
using SixLabors.ImageSharp.PixelFormats;

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

        public static async Task UploadMediaAsync(IFileStorageProvider fileStorageProvider, string fileName, ImageRgba32 image, int max = 1280)
        {
            //Fixed in passing, on September 3rd 2026: this overload computed the cleaned image
            //then serialized `image`, the original. The resize was therefore computed and thrown
            //away.
            var imageModified = ImageUtils.CleanImage(image, max, out _);
            try
            {
                var bytes = ImageSerialization.SaveJpegToBytes(imageModified);
                using var ms = new MemoryStream(bytes);
                await fileStorageProvider.WriteAsync(fileName, ms, GetContentTypeFromFileName(fileName));
            }
            finally
            {
                //CleanImage returns the original when there is nothing to do: dispose only what it
                //created.
                if (!ReferenceEquals(imageModified, image))
                    imageModified.Dispose();
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
