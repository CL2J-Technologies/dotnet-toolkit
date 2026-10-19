using SkiaSharp;

namespace cl2j.Image
{
    public class ImageSerialization
    {
        public static void SaveJpeg(string path, RasterImage image, long quality = 75L)
        {
            File.WriteAllBytes(path, SaveJpegToBytes(image, quality));
        }

        public static Stream SaveJpegToStream(RasterImage image, long quality = 75L)
        {
            return new MemoryStream(SaveJpegToBytes(image, quality));
        }

        /// <summary>
        /// The image as a JPEG. No metadata is written — no EXIF, no ICC profile — which is what
        /// `ExifUtils.Strip` promises. Alpha is ignored: JPEG has none, and a transparent pixel comes
        /// out as its colour channels, black for transparent black, as with the previous engine.
        /// </summary>
        public static byte[] SaveJpegToBytes(RasterImage image, long quality = 75L)
        {
            using var skImage = SKImage.FromBitmap(image.Bitmap);
            using var data = skImage.Encode(SKEncodedImageFormat.Jpeg, (int)Math.Clamp(quality, 1, 100))
                ?? throw new InvalidOperationException($"JPEG encoding failed for a {image.Width} x {image.Height} image");
            return data.ToArray();
        }

        /// <summary>
        /// The image as a lossy WebP, on the same quality scale as <see cref="SaveJpegToBytes"/>. No
        /// metadata is written, as for the JPEG. Alpha is kept: unlike JPEG, WebP has a channel for
        /// it.
        ///
        /// Added in 6.3.0 for Appartogo, which serves its listing photos as WebP next to the JPEG.
        /// SkiaSharp encodes it natively (libwebp): no library is added, and AVIF stays out of reach.
        ///
        /// Skia switches to **lossless** WebP at quality 100, which weighs more than the JPEG would.
        /// The number is clamped to 1..100 like the JPEG's, and the default stays 75.
        /// </summary>
        public static byte[] SaveWebpToBytes(RasterImage image, long quality = 75L)
        {
            using var skImage = SKImage.FromBitmap(image.Bitmap);
            using var data = skImage.Encode(SKEncodedImageFormat.Webp, (int)Math.Clamp(quality, 1, 100))
                ?? throw new InvalidOperationException($"WebP encoding failed for a {image.Width} x {image.Height} image");
            return data.ToArray();
        }
    }
}
