using SkiaSharp;

namespace cl2j.Image
{
    public static class ExifUtils
    {
        /// <summary>
        /// Applies the EXIF orientation still pending on the image, then marks it upright. Returns
        /// true if the pixels moved. A phone held sideways stores its photo lying down with
        /// orientation 6 or 8; served as-is, the flat appears on its side.
        ///
        /// The pixels are remapped exactly, never resampled: a rotation by a multiple of 90 degrees
        /// or a mirror loses nothing.
        /// </summary>
        public static bool RotateFlipIfRequired(RasterImage image)
        {
            var origin = image.Origin;
            if (origin == SKEncodedOrigin.TopLeft)
                return false;

            image.Replace(Orient(image.Bitmap, origin));
            image.Origin = SKEncodedOrigin.TopLeft;
            return true;
        }

        /// <summary>
        /// Forgets the metadata the file carried — EXIF, XMP, IPTC, ICC profile — so that the next
        /// encoding writes none. Returns true if there was any. The encoder of this library never
        /// writes metadata: this flag is what tells `ImageUtils.CleanImage` that re-encoding is
        /// needed to get rid of it.
        /// </summary>
        public static bool Strip(RasterImage image)
        {
            var had = image.HasMetadata;
            image.HasMetadata = false;
            return had;
        }

        // Where the stored pixel (x, y) goes in the upright image, per the EXIF definition of each
        // orientation: which visual side row 0 and column 0 of the stored image are.
        private static SKBitmap Orient(SKBitmap source, SKEncodedOrigin origin)
        {
            int w = source.Width, h = source.Height;
            var swaps = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
            int tw = swaps ? h : w, th = swaps ? w : h;

            var target = new SKBitmap(RasterImage.Info(tw, th));
            var src = source.GetPixelSpan();
            var dst = target.GetPixelSpan();
            for (int y = 0; y < h; ++y)
            {
                for (int x = 0; x < w; ++x)
                {
                    var (tx, ty) = origin switch
                    {
                        SKEncodedOrigin.TopRight => (w - 1 - x, y),            // 2: mirrored horizontally
                        SKEncodedOrigin.BottomRight => (w - 1 - x, h - 1 - y), // 3: rotated 180
                        SKEncodedOrigin.BottomLeft => (x, h - 1 - y),          // 4: mirrored vertically
                        SKEncodedOrigin.LeftTop => (y, x),                     // 5: transposed
                        SKEncodedOrigin.RightTop => (h - 1 - y, x),            // 6: rotated 90 clockwise
                        SKEncodedOrigin.RightBottom => (h - 1 - y, w - 1 - x), // 7: transversed
                        SKEncodedOrigin.LeftBottom => (y, w - 1 - x),          // 8: rotated 90 counter-clockwise
                        _ => (x, y),
                    };
                    var s = (y * w + x) * 4;
                    var d = (ty * tw + tx) * 4;
                    dst[d] = src[s];
                    dst[d + 1] = src[s + 1];
                    dst[d + 2] = src[s + 2];
                    dst[d + 3] = src[s + 3];
                }
            }
            return target;
        }
    }
}
