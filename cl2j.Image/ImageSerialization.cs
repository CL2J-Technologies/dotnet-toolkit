using ImageRgba32 = SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;

namespace cl2j.Image
{
    public class ImageSerialization
    {
        //Ported to ImageSharp on September 3rd 2026, see ImageResizer for why.
        //
        //`quality` stays a `long` so as not to break callers, which all pass `75L`. ImageSharp
        //expects an integer from 1 to 100, as the GDI+ JPEG encoder did.
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
