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
            //Corrige au passage, le 3 septembre 2026 : cette surcharge calculait l image nettoyee
            //puis serialisait `image`, l originale. Le redimensionnement etait donc calcule et jete.
            var imageModified = ImageUtils.CleanImage(image, max, out _);
            try
            {
                var bytes = ImageSerialization.SaveJpegToBytes(imageModified);
                using var ms = new MemoryStream(bytes);
                await fileStorageProvider.WriteAsync(fileName, ms, GetContentTypeFromFileName(fileName));
            }
            finally
            {
                //CleanImage rend l originale quand il n y a rien a faire : ne liberer que ce qu il
                //a cree.
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
