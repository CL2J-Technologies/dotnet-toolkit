using ImageRgba32 = SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;

namespace cl2j.Image
{
    public class ImageSerialization
    {
        //Porte sur ImageSharp le 3 septembre 2026, voir ImageResizer pour le pourquoi.
        //
        //`quality` reste un `long` pour ne pas casser les appelants, qui passent tous `75L`.
        //ImageSharp attend un entier de 1 a 100, comme le faisait l encodeur JPEG de GDI+.
        public static void SaveJpeg(string path, ImageRgba32 image, long quality = 75L)
        {
            image.Save(path, Encoder(quality));
        }

        public static Stream SaveJpegToStream(ImageRgba32 image, long quality = 75L)
        {
            var ms = new MemoryStream();
            image.Save(ms, Encoder(quality));
            ms.Position = 0;
            return ms;
        }

        public static byte[] SaveJpegToBytes(ImageRgba32 image, long quality = 75L)
        {
            using var ms = new MemoryStream();
            image.Save(ms, Encoder(quality));
            return ms.ToArray();
        }

        private static JpegEncoder Encoder(long quality)
        {
            return new JpegEncoder { Quality = (int)Math.Clamp(quality, 1, 100) };
        }
    }
}
