using cl2j.Image;
using SkiaSharp;
using Xunit;

namespace cl2j.Image.Tests
{
    /// <summary>
    /// These tests exist because of a precise failure: `cl2j.Image` relied on `System.Drawing`,
    /// which throws `PlatformNotSupportedException` off Windows since .NET 6. The Appartogo site
    /// runs on Linux, so its portal produced **no thumbnail at all** between June 2025 and
    /// September 2026 — 5,400 images, zero thumbnails, without one line of error, because the
    /// entry points swallowed the exception to return `null` or the original bytes.
    ///
    /// CI runs `dotnet test` on `ubuntu-latest`. These tests therefore execute on exactly the
    /// system where the failure occurred: they would catch it on the first try.
    ///
    /// Since 6.0.0 the library runs on SkiaSharp instead of ImageSharp (see the package
    /// description). The tests build their images with SkiaSharp and, where a behaviour is defined
    /// by a standard rather than by a library — EXIF orientation — they assert the standard.
    /// </summary>
    public class ImageUtilsTests
    {
        [Fact]
        public void CleanImage_resizes_an_oversized_image()
        {
            var bytes = Jpeg(4000, 2252);

            var cleaned = ImageUtils.CleanImage(bytes, 1280);

            Assert.NotNull(cleaned);
            using var image = ImageUtils.ReadImage(cleaned!);
            Assert.NotNull(image);
            Assert.Equal(1280, image!.Width);
            Assert.True(image.Height <= 1280);
            Assert.True(cleaned!.Length < bytes.Length, "the cleaned image must weigh less than the original");
        }

        [Fact]
        public void CleanImage_leaves_the_dimensions_of_an_already_small_image()
        {
            var bytes = Jpeg(640, 480);

            var cleaned = ImageUtils.CleanImage(bytes, 1280);

            using var image = ImageUtils.ReadImage(cleaned!);
            Assert.Equal(640, image!.Width);
            Assert.Equal(480, image.Height);
        }

        [Fact]
        public void CleanImage_throws_on_unreadable_bytes()
        {
            //Silence was the defect: the old version returned the original bytes, so an unreadable
            //image was stored as-is without anyone knowing.
            Assert.ThrowsAny<Exception>(() => ImageUtils.CleanImage([1, 2, 3, 4, 5], 1280));
        }

        [Fact]
        public void CreateThumbnailCropped_returns_a_thumbnail_450x337_for_a_640x480()
        {
            //Dimensions taken on September 3rd 2026 from a real CDN thumbnail, produced by the old
            //System.Drawing implementation from a 640x480 source. Every port must return exactly
            //the same geometry.
            using var source = new RasterImage(640, 480);

            using var thumbnail = ImageUtils.CreateThumbnailCropped(source, 450, 338);

            Assert.Equal(450, thumbnail.Width);
            Assert.Equal(337, thumbnail.Height);
        }

        [Fact]
        public void CreateThumbnailCropped_produces_a_readable_jpeg()
        {
            using var source = ImageUtils.ReadImage(Jpeg(4000, 2252))!;

            using var thumbnail = ImageUtils.CreateThumbnailCropped(source, 450, 338);
            var bytes = ImageSerialization.SaveJpegToBytes(thumbnail, 75L);

            Assert.NotEmpty(bytes);
            Assert.Equal("JPEG", ImageUtils.Identify(bytes)?.Format);

            using var reread = ImageUtils.ReadImage(bytes);
            Assert.NotNull(reread);
            Assert.Equal(450, reread!.Width);
            Assert.Equal(338, reread.Height);
        }

        [Fact]
        public void ReadImage_returns_null_on_unreadable_bytes()
        {
            Assert.Null(ImageUtils.ReadImage([1, 2, 3, 4, 5]));
        }

        [Fact]
        public void IsImage_recognises_a_jpeg()
        {
            Assert.True(ImageUtils.IsImage(Jpeg(64, 48)));
        }

        [Theory]
        [InlineData("<!DOCTYPE html>\r\n<html lang=\"fr\"><head><title>LogisQuebec</title></head></html>")]
        [InlineData("")]
        [InlineData("not an image at all")]
        public void IsImage_refuses_what_is_not_an_image(string content)
        {
            //The case that matters is the first: a source that redirects its deleted photos to its
            //home page returns HTML with a 200, and HttpClient follows the redirect on its own.
            //Without this guard, the page is stored under a .jpg name.
            Assert.False(ImageUtils.IsImage(System.Text.Encoding.UTF8.GetBytes(content)));
        }

        [Fact]
        public void IsImage_refuses_missing_bytes()
        {
            Assert.False(ImageUtils.IsImage(null!));
        }

        [Fact]
        public void Identify_reads_the_header_without_decoding()
        {
            var header = ImageUtils.Identify(Jpeg(320, 240));

            Assert.NotNull(header);
            Assert.Equal(320, header!.Width);
            Assert.Equal(240, header.Height);
            Assert.Equal(1, header.FrameCount);
            Assert.Equal("JPEG", header.Format);
        }

        [Fact]
        public void Identify_returns_null_for_what_is_not_an_image()
        {
            Assert.Null(ImageUtils.Identify([1, 2, 3, 4, 5]));
            Assert.Null(ImageUtils.Identify([]));
            Assert.Null(ImageUtils.Identify(null!));
        }

        // --- Metadata and EXIF orientation -------------------------------------------------------

        [Fact]
        public void Strip_removes_the_metadata()
        {
            using var image = ImageUtils.ReadImage(WithExif(Jpeg(10, 10), orientation: 1))!;

            Assert.True(ExifUtils.Strip(image));
            Assert.False(ExifUtils.Strip(image));
        }

        [Fact]
        public void Strip_reports_nothing_on_an_image_without_metadata()
        {
            using var image = ImageUtils.ReadImage(Jpeg(10, 10))!;

            Assert.False(ExifUtils.Strip(image));
        }

        /// <summary>
        /// Where a red block drawn in the top-left corner of the stored pixels must appear once the
        /// image is displayed upright, for each EXIF orientation. These are the definitions of the
        /// EXIF standard (row 0 / column 0 of the stored image), not the behaviour of a library:
        /// a phone held sideways writes 6 or 8, and the portal receives such photos every day.
        /// </summary>
        [Theory]
        [InlineData(2, "top-right", false)]
        [InlineData(3, "bottom-right", false)]
        [InlineData(4, "bottom-left", false)]
        [InlineData(5, "top-left", true)]
        [InlineData(6, "top-right", true)]
        [InlineData(7, "bottom-right", true)]
        [InlineData(8, "bottom-left", true)]
        public void RotateFlipIfRequired_follows_the_EXIF_orientation(int orientation, string corner, bool swapsDimensions)
        {
            using var image = ImageUtils.ReadImage(WithExif(JpegWithRedCorner(120, 60), orientation))!;
            Assert.Equal(120, image.Width);

            Assert.True(ExifUtils.RotateFlipIfRequired(image));

            Assert.Equal(swapsDimensions ? 60 : 120, image.Width);
            Assert.Equal(swapsDimensions ? 120 : 60, image.Height);
            Assert.Equal(corner, RedCorner(image));
            Assert.False(ExifUtils.RotateFlipIfRequired(image));
        }

        [Fact]
        public void RotateFlipIfRequired_leaves_an_already_upright_image_alone()
        {
            using var image = ImageUtils.ReadImage(WithExif(JpegWithRedCorner(120, 60), orientation: 1))!;

            Assert.False(ExifUtils.RotateFlipIfRequired(image));
            Assert.Equal(120, image.Width);
            Assert.Equal("top-left", RedCorner(image));
        }

        [Fact]
        public void CleanImage_turns_a_sideways_photo_upright()
        {
            var bytes = WithExif(JpegWithRedCorner(120, 60), orientation: 6);

            using var cleaned = ImageUtils.ReadImage(ImageUtils.CleanImage(bytes, 1280)!)!;

            Assert.Equal(60, cleaned.Width);
            Assert.Equal(120, cleaned.Height);
            Assert.Equal("top-right", RedCorner(cleaned));
            Assert.False(ExifUtils.RotateFlipIfRequired(cleaned), "the re-encoded image must not be rotated a second time");
        }

        // --- Formats not rendered by browsers ----------------------------------------------------
        //
        // Context: of the 124 files the portal backfill refused on September 4th 2026, 93 were
        // HEIC and 2 were AVIF — iPhone photos uploaded as-is and stored under a `.jpg` name.
        // Client decision the same day: **no native library** will be added to decode them; the
        // conversion will happen in the browser. The server, for its part, must refuse cleanly and
        // be able to *name* what it refuses.
        //
        // BMP serves as the control here: the decoder reads it, but it is not on the list of web
        // formats. That is exactly the case the re-encoding rule must catch.

        [Fact]
        public void CleanImage_reencodes_a_format_outside_the_web_list()
        {
            // A 320 x 240 image exceeds no dimension and has no EXIF to straighten: only the
            // format can trigger the re-encoding.
            var bytes = Bmp(320, 240);

            var cleaned = ImageUtils.CleanImage(bytes, 1280);

            Assert.NotNull(cleaned);
            Assert.Equal("JPEG", ImageUtils.Identify(cleaned!)?.Format);
        }

        [Fact]
        public void CleanImage_does_not_reencode_an_already_compliant_JPEG()
        {
            // The counterpart of the previous test: the rule must not needlessly recompress what is
            // already served correctly.
            var bytes = Jpeg(320, 240);

            Assert.Same(bytes, ImageUtils.CleanImage(bytes, 1280));
        }

        [Fact]
        public void CleanImage_does_not_reencode_an_already_compliant_WebP()
        {
            var bytes = WebP(320, 240);

            Assert.Same(bytes, ImageUtils.CleanImage(bytes, 1280));
        }

        [Fact]
        public void CleanImage_reencodes_a_JPEG_that_carries_metadata_and_drops_it()
        {
            // An upright photo still carries its EXIF block — camera, date, sometimes the GPS
            // position of the flat. Stripping it requires re-encoding.
            var bytes = WithExif(Jpeg(320, 240), orientation: 1);

            var cleaned = ImageUtils.CleanImage(bytes, 1280)!;

            Assert.NotSame(bytes, cleaned);
            using var reread = ImageUtils.ReadImage(cleaned)!;
            Assert.False(ExifUtils.Strip(reread), "the re-encoded image must carry no metadata");
        }

        [Fact]
        public void TIFF_is_named_and_refused()
        {
            // ImageSharp read TIFF; SkiaSharp does not. A TIFF upload therefore fails to read — and
            // the failure says which format it was, like HEIC.
            var tiff = new byte[] { (byte)'I', (byte)'I', 42, 0, 8, 0, 0, 0, 0, 0, 0, 0 };

            Assert.Equal("TIFF", ImageUtils.NameUnsupportedFormat(tiff));
            Assert.Null(ImageUtils.ReadImage(tiff, out var failure));
            Assert.Contains("TIFF", failure);
        }

        [Theory]
        [InlineData("heic", "HEIC")]
        [InlineData("mif1", "HEIC")]
        [InlineData("avif", "AVIF")]
        [InlineData("qt  ", "video QuickTime")]
        [InlineData("mp42", "video MP4")]
        public void NameUnsupportedFormat_recognises_the_portal_brands(string brand, string expected)
        {
            // The five families actually found in listing-portal. Without this name, the portal
            // cannot tell the user why their photo was refused — and a mute refusal is precisely
            // what cost fifteen months of missing thumbnails.
            Assert.Equal(expected, ImageUtils.NameUnsupportedFormat(IsoBmffBox(brand)));
        }

        [Fact]
        public void NameUnsupportedFormat_does_not_name_what_it_does_not_recognise()
        {
            Assert.Null(ImageUtils.NameUnsupportedFormat(Jpeg(32, 32)));
            Assert.Null(ImageUtils.NameUnsupportedFormat(IsoBmffBox("zzzz")));
            Assert.Null(ImageUtils.NameUnsupportedFormat([1, 2, 3]));
            Assert.Null(ImageUtils.NameUnsupportedFormat(null!));
        }

        [Fact]
        public void ReadImage_says_why_it_could_not_read()
        {
            //What the silence cost: fifteen months of missing thumbnails where the caller knew it
            //had failed and no one knew the decoder simply does not read HEIC.
            Assert.Null(ImageUtils.ReadImage(IsoBmffBox("heic"), out var heic));
            Assert.Contains("HEIC", heic);

            Assert.Null(ImageUtils.ReadImage([1, 2, 3, 4, 5], out var garbage));
            Assert.False(string.IsNullOrWhiteSpace(garbage));

            Assert.Null(ImageUtils.ReadImage([], out var empty));
            Assert.Equal("empty", empty);

            Assert.Null(ImageUtils.ReadImage(null!, out var none));
            Assert.Equal("no bytes", none);
        }

        [Fact]
        public void ReadImage_says_nothing_when_it_reads()
        {
            using var image = ImageUtils.ReadImage(Jpeg(64, 48), out var failure);

            Assert.NotNull(image);
            Assert.Null(failure);
        }

        // --- Resizing ----------------------------------------------------------------------------

        [Fact]
        public void A_strong_downscale_does_not_alias()
        {
            // Measured while choosing SkiaSharp, October 2026: a single cubic pass from 5760 to
            // 1280 pixels folded the fine detail of real photos back into visible patterns. A
            // one-pixel checkerboard is the worst case: correctly filtered, it averages to a flat
            // mid grey; aliased, it comes back as stripes.
            using var checkerboard = new RasterImage(4000, 2000);
            checkerboard.Fill((x, y) => (x + y) % 2 == 0 ? (byte)0 : (byte)255);

            using var small = ImageResizer.ResizeIfOversize(checkerboard, 1000, 1000);

            Assert.Equal(1000, small.Width);
            Assert.True(GreyStandardDeviation(small) < 12, $"standard deviation {GreyStandardDeviation(small):F1}");
        }

        [Fact]
        public void Resize_to_the_same_size_returns_the_same_instance()
        {
            // Callers dispose what they did not receive back: the identity is part of the contract.
            using var image = new RasterImage(200, 100);

            Assert.Same(image, ImageResizer.Resize(image, 200, 100));
        }

        // --- Duplicate detection -----------------------------------------------------------------

        [Fact]
        public void AreImagesIdentical_recognises_the_same_photo_at_another_size()
        {
            using var photo = ImageUtils.ReadImage(Jpeg(800, 600))!;
            using var half = ImageResizer.Resize(photo, 400, 300);
            using var copy = ImageUtils.ReadImage(ImageSerialization.SaveJpegToBytes(half, 75L))!;

            Assert.True(ImageUtils.AreImagesIdentical(photo, photo, new ImageUtils.ImageCompareSettings()));
            Assert.True(ImageUtils.AreImagesIdentical(photo, copy, new ImageUtils.ImageCompareSettings()));
        }

        [Fact]
        public void AreImagesIdentical_tells_two_different_photos_apart()
        {
            using var photo = ImageUtils.ReadImage(Jpeg(800, 600))!;
            using var other = ImageUtils.ReadImage(JpegWithRedCorner(800, 600))!;

            Assert.False(ImageUtils.AreImagesIdentical(photo, other, new ImageUtils.ImageCompareSettings()));
        }

        // --- Helpers -----------------------------------------------------------------------------

        private static byte[] IsoBmffBox(string brand)
        {
            // Four bytes of size, the `ftyp` tag, then the brand: the header of an ISO-BMFF
            // container. What follows does not matter, nothing decodes it.
            var bytes = new byte[16];
            bytes[3] = 16;
            System.Text.Encoding.ASCII.GetBytes("ftyp").CopyTo(bytes, 4);
            System.Text.Encoding.ASCII.GetBytes(brand).CopyTo(bytes, 8);
            return bytes;
        }

        //A gradient rather than a flat image: a flat JPEG compresses to almost nothing, and the
        //size-reduction test would then prove very little.
        private static SKBitmap Gradient(int width, int height)
        {
            var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
            for (var y = 0; y < height; ++y)
            {
                for (var x = 0; x < width; ++x)
                    bitmap.SetPixel(x, y, new SKColor((byte)(x % 256), (byte)(y % 256), (byte)((x + y) % 256)));
            }
            return bitmap;
        }

        private static byte[] Encode(SKBitmap bitmap, SKEncodedImageFormat format, int quality = 90)
        {
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(format, quality);
            return data.ToArray();
        }

        private static byte[] Jpeg(int width, int height)
        {
            using var bitmap = Gradient(width, height);
            return Encode(bitmap, SKEncodedImageFormat.Jpeg);
        }

        private static byte[] WebP(int width, int height)
        {
            using var bitmap = Gradient(width, height);
            return Encode(bitmap, SKEncodedImageFormat.Webp);
        }

        /// <summary>A grey image with a pure red 20 x 20 block in the top-left corner of the stored pixels.</summary>
        private static byte[] JpegWithRedCorner(int width, int height)
        {
            using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
            for (var y = 0; y < height; ++y)
            {
                for (var x = 0; x < width; ++x)
                    bitmap.SetPixel(x, y, x < 20 && y < 20 ? new SKColor(255, 0, 0) : new SKColor(128, 128, 128));
            }
            return Encode(bitmap, SKEncodedImageFormat.Jpeg, 95);
        }

        /// <summary>
        /// Inserts an EXIF APP1 segment right after the JPEG start marker, holding a single
        /// Orientation tag. Built by hand so the test depends on the EXIF format, not on a library
        /// able to write it.
        /// </summary>
        private static byte[] WithExif(byte[] jpeg, int orientation)
        {
            var tiff = new List<byte>
            {
                (byte)'I', (byte)'I', 42, 0, 8, 0, 0, 0,     // little-endian TIFF header, IFD0 at offset 8
                1, 0,                                        // one entry
                0x12, 0x01, 3, 0, 1, 0, 0, 0,                // tag 0x0112 Orientation, type SHORT, count 1
                (byte)orientation, 0, 0, 0,                  // value
                0, 0, 0, 0,                                  // no next IFD
            };
            var payload = System.Text.Encoding.ASCII.GetBytes("Exif\0\0").Concat(tiff).ToArray();
            var length = payload.Length + 2;
            var segment = new byte[] { 0xFF, 0xE1, (byte)(length >> 8), (byte)(length & 0xFF) }.Concat(payload);
            return jpeg.Take(2).Concat(segment).Concat(jpeg.Skip(2)).ToArray();
        }

        /// <summary>A 24-bit bottom-up BMP, written by hand: SkiaSharp reads BMP but does not encode it.</summary>
        private static byte[] Bmp(int width, int height)
        {
            var rowSize = (width * 3 + 3) & ~3;
            var pixels = rowSize * height;
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write((byte)'B'); w.Write((byte)'M'); w.Write(54 + pixels); w.Write(0); w.Write(54);
            w.Write(40); w.Write(width); w.Write(height); w.Write((short)1); w.Write((short)24);
            w.Write(0); w.Write(pixels); w.Write(2835); w.Write(2835); w.Write(0); w.Write(0);
            for (var y = 0; y < height; ++y)
            {
                for (var x = 0; x < width; ++x)
                {
                    w.Write((byte)(x % 256)); w.Write((byte)(y % 256)); w.Write((byte)((x + y) % 256));
                }
                for (var p = width * 3; p < rowSize; ++p)
                    w.Write((byte)0);
            }
            w.Flush();
            return ms.ToArray();
        }

        /// <summary>The corner whose 10 x 10 patch is clearly red — JPEG blurs the edges, not the inside.</summary>
        private static string RedCorner(RasterImage image)
        {
            bool Red(int x0, int y0)
            {
                for (var y = y0; y < y0 + 10; ++y)
                {
                    for (var x = x0; x < x0 + 10; ++x)
                    {
                        var (r, g, b) = image.GetPixel(x, y);
                        if (r < 200 || g > 60 || b > 60)
                            return false;
                    }
                }
                return true;
            }

            var corners = new List<string>();
            if (Red(2, 2)) corners.Add("top-left");
            if (Red(image.Width - 12, 2)) corners.Add("top-right");
            if (Red(2, image.Height - 12)) corners.Add("bottom-left");
            if (Red(image.Width - 12, image.Height - 12)) corners.Add("bottom-right");
            return string.Join(",", corners);
        }

        private static double GreyStandardDeviation(RasterImage image)
        {
            var values = new List<double>();
            for (var y = 0; y < image.Height; y += 3)
            {
                for (var x = 0; x < image.Width; x += 3)
                {
                    var (r, g, b) = image.GetPixel(x, y);
                    values.Add(0.3 * r + 0.59 * g + 0.11 * b);
                }
            }
            var mean = values.Average();
            return Math.Sqrt(values.Average(v => (v - mean) * (v - mean)));
        }
    }
}
