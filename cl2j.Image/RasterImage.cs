using SkiaSharp;

namespace cl2j.Image
{
    /// <summary>
    /// A decoded image: 8-bit RGBA pixels, unpremultiplied, plus what the file said about them that
    /// still matters after decoding — the EXIF orientation not applied yet, and whether metadata was
    /// present.
    ///
    /// **Why the library has its own type.** Up to 5.x the public API handed out
    /// `SixLabors.ImageSharp.Image&lt;Rgba32&gt;`, so every consumer compiled against ImageSharp and
    /// could not move off it without a rewrite. Version 6.0.0 moved to SkiaSharp; this type keeps
    /// SkiaSharp behind the API, so the next change of engine stays inside this package.
    ///
    /// Orientation is kept pending, not applied at decode, on purpose: callers read `Width` and
    /// `Height` as the file stores them, then decide — `ExifUtils.RotateFlipIfRequired`, which
    /// `ImageUtils.CleanImage` calls — exactly as they did with the previous engine.
    /// </summary>
    public sealed class RasterImage : IDisposable
    {
        private SKBitmap bitmap;

        /// <summary>A blank image, every pixel transparent black.</summary>
        public RasterImage(int width, int height)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), $"An image needs positive dimensions, got {width} x {height}.");

            bitmap = new SKBitmap(Info(width, height));
            bitmap.Erase(SKColors.Transparent);
        }

        internal RasterImage(SKBitmap bitmap, SKEncodedOrigin origin = SKEncodedOrigin.TopLeft, bool hasMetadata = false)
        {
            this.bitmap = bitmap;
            Origin = origin;
            HasMetadata = hasMetadata;
        }

        public int Width => bitmap.Width;

        public int Height => bitmap.Height;

        /// <summary>The EXIF orientation still to apply. `TopLeft` means upright.</summary>
        internal SKEncodedOrigin Origin { get; set; }

        /// <summary>True while the image carries metadata from its file — EXIF, XMP, IPTC or an ICC profile.</summary>
        internal bool HasMetadata { get; set; }

        internal SKBitmap Bitmap => bitmap;

        /// <summary>The colour of one pixel, alpha ignored.</summary>
        public (byte R, byte G, byte B) GetPixel(int x, int y)
        {
            var c = bitmap.GetPixel(x, y);
            return (c.Red, c.Green, c.Blue);
        }

        public RasterImage Clone() => new(bitmap.Copy(), Origin, HasMetadata);

        public void Dispose() => bitmap.Dispose();

        /// <summary>Swaps the pixels for new ones, disposing the old ones.</summary>
        internal void Replace(SKBitmap replacement)
        {
            if (ReferenceEquals(replacement, bitmap))
                return;
            var old = bitmap;
            bitmap = replacement;
            old.Dispose();
        }

        /// <summary>Fills the image with a grey level computed per pixel. Used by tests to build exact patterns.</summary>
        internal void Fill(Func<int, int, byte> grey)
        {
            var pixels = bitmap.GetPixelSpan();
            for (var y = 0; y < Height; ++y)
            {
                for (var x = 0; x < Width; ++x)
                {
                    var g = grey(x, y);
                    var i = (y * Width + x) * 4;
                    pixels[i] = g;
                    pixels[i + 1] = g;
                    pixels[i + 2] = g;
                    pixels[i + 3] = 255;
                }
            }
        }

        /// <summary>The single pixel layout of the library: RGBA, 8 bits per channel, unpremultiplied.</summary>
        internal static SKImageInfo Info(int width, int height) => new(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
    }

    /// <summary>What an image file declares in its header, read without decoding the pixels.</summary>
    /// <param name="Width">Width as stored, before any EXIF orientation.</param>
    /// <param name="Height">Height as stored, before any EXIF orientation.</param>
    /// <param name="FrameCount">1 for a still image, more for an animation.</param>
    /// <param name="Format">Upper-case format name: JPEG, PNG, WEBP, GIF, BMP, ICO, WBMP…</param>
    public sealed record ImageHeader(int Width, int Height, int FrameCount, string Format);

    /// <summary>An opaque colour, for the background of a letterboxed thumbnail.</summary>
    public readonly record struct RgbColor(byte R, byte G, byte B)
    {
        public static RgbColor White => new(255, 255, 255);
    }
}
