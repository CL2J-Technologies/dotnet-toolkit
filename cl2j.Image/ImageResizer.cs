using SkiaSharp;

namespace cl2j.Image
{
    public class ImageResizer
    {
        //Catmull-Rom: the bicubic kernel ImageSharp used under the name `Bicubic`, kept so the
        //thumbnails do not change character with the engine.
        private static readonly SKSamplingOptions Bicubic = new(SKCubicResampler.CatmullRom);
        private static readonly SKSamplingOptions Linear = new(SKFilterMode.Linear, SKMipmapMode.None);

        /// <summary>
        /// The image scaled to `newWidth` x `newHeight`, aspect ratio not preserved. Returns **the
        /// same instance** when the size already matches: callers rely on it to know what to dispose.
        ///
        /// **Why the halving.** A single cubic pass samples only a few source pixels per target
        /// pixel: past a 2x reduction, the fine detail folds back into patterns that were not in the
        /// photo. Measured in October 2026 on real listing photos, a 5760-pixel image reduced to
        /// 1280 in one pass differed by 10 grey levels on average from the reference; halving first,
        /// with a linear filter, while the image is still more than twice the target brought it to 6
        /// and made it indistinguishable. ImageSharp did the equivalent by widening its kernel with
        /// the reduction factor.
        /// </summary>
        public static RasterImage Resize(RasterImage image, int newWidth, int newHeight)
        {
            if (image.Width == newWidth && image.Height == newHeight)
                return image;

            var current = image.Bitmap;
            while (current.Width >= 2 * newWidth && current.Height >= 2 * newHeight)
            {
                var half = current.Resize(RasterImage.Info(current.Width / 2, current.Height / 2), Linear)
                    ?? throw new InvalidOperationException($"Cannot halve a {current.Width} x {current.Height} image");
                if (!ReferenceEquals(current, image.Bitmap))
                    current.Dispose();
                current = half;
            }

            var resized = current.Resize(RasterImage.Info(newWidth, newHeight), Bicubic)
                ?? throw new InvalidOperationException($"Cannot resize a {current.Width} x {current.Height} image to {newWidth} x {newHeight}");
            if (!ReferenceEquals(current, image.Bitmap))
                current.Dispose();

            return new RasterImage(resized, image.Origin, image.HasMetadata);
        }

        /// <summary>
        /// The image scaled down, aspect ratio kept, until both dimensions fit within `maxW` x
        /// `maxH`. An image that already fits is never enlarged: it is returned as **the same
        /// instance**, so callers must compare with `ReferenceEquals` before disposing the result.
        /// </summary>
        public static RasterImage ResizeIfOversize(RasterImage image, int maxW, int maxH)
        {
            if (image.Width <= maxW && image.Height <= maxH)
                return image;

            //The tighter limit decides, and it is hit exactly; the other dimension follows the
            //aspect ratio. Compared by cross-multiplying, in long, so that nothing is rounded or
            //overflows before the choice is made.
            int newW;
            int newH;
            if ((long)image.Width * maxH > (long)image.Height * maxW)
            {
                newW = maxW;
                newH = (int)((long)image.Height * maxW / image.Width);
            }
            else
            {
                newH = maxH;
                newW = (int)((long)image.Width * maxH / image.Height);
            }

            return Resize(image, Math.Max(1, newW), Math.Max(1, newH));
        }

        /// <summary>
        /// The image scaled so that its longer side matches the limit, **enlarging** it when it is
        /// smaller. The thumbnail builders rely on that to fill a fixed-size frame; use
        /// <see cref="ResizeIfOversize"/> to only ever reduce.
        /// </summary>
        internal static RasterImage ScaleToLongestSide(RasterImage image, int maxW, int maxH)
        {
            int newW;
            int newH;
            if (image.Width > image.Height)
            {
                newW = maxW;
                newH = image.Height * maxW / image.Width;
            }
            else
            {
                newW = image.Width * maxH / image.Height;
                newH = maxH;
            }

            return Resize(image, Math.Max(1, newW), Math.Max(1, newH));
        }
    }
}
