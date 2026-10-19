using System.Text;
using cl2j.Image;
using SkiaSharp;
using Xunit;

namespace cl2j.Image.Tests
{
    /// <summary>
    /// The WebP encoder, added in 6.3.0 so that Appartogo can serve its listing photos as WebP next
    /// to the JPEG. `RasterImage.Bitmap` is internal, so a consumer cannot encode on its own: the
    /// package has to.
    /// </summary>
    public class ImageSerializationTests
    {
        [Fact]
        public void SaveWebpToBytes_writes_a_riff_webp_container()
        {
            using var image = Photo(320, 240);

            var bytes = ImageSerialization.SaveWebpToBytes(image);

            //RIFF, a four-byte size, then WEBP: what any WebP reader checks first.
            Assert.True(bytes.Length > 12);
            Assert.Equal("RIFF", Encoding.ASCII.GetString(bytes, 0, 4));
            Assert.Equal("WEBP", Encoding.ASCII.GetString(bytes, 8, 4));
        }

        [Fact]
        public void SaveWebpToBytes_keeps_the_dimensions()
        {
            using var image = Photo(450, 338);

            var header = ImageUtils.Identify(ImageSerialization.SaveWebpToBytes(image));

            Assert.NotNull(header);
            Assert.Equal("WEBP", header!.Format);
            Assert.Equal(450, header.Width);
            Assert.Equal(338, header.Height);
        }

        [Fact]
        public void SaveWebpToBytes_reads_back_as_the_image_it_was_given()
        {
            using var image = Photo(320, 240);

            using var reread = ImageUtils.ReadImage(ImageSerialization.SaveWebpToBytes(image, 90L))!;

            Assert.True(ImageUtils.Compare(image, reread, out var diff));
            Assert.True(diff <= 3, $"the mean grey difference after a WebP round trip was {diff}");
        }

        [Fact]
        public void SaveWebpToBytes_weighs_less_at_a_lower_quality()
        {
            using var image = Photo(640, 480);

            var high = ImageSerialization.SaveWebpToBytes(image, 95L);
            var low = ImageSerialization.SaveWebpToBytes(image, 40L);

            Assert.True(low.Length < high.Length, $"quality 40 gave {low.Length} bytes, quality 95 gave {high.Length}");
        }

        [Theory]
        [InlineData(0L)]
        [InlineData(-5L)]
        [InlineData(1000L)]
        public void SaveWebpToBytes_clamps_a_quality_outside_one_to_one_hundred(long quality)
        {
            //Same contract as SaveJpegToBytes: an out-of-range number is brought back, not thrown.
            using var image = Photo(64, 48);

            var bytes = ImageSerialization.SaveWebpToBytes(image, quality);

            Assert.Equal("WEBP", ImageUtils.Identify(bytes)?.Format);
        }

        [Fact]
        public void SaveWebpToBytes_writes_no_metadata()
        {
            //As SaveJpegToBytes: no EXIF, no XMP, no ICC profile. A WebP carries them in the
            //EXIF, XMP and ICCP chunks of its container.
            using var image = Photo(64, 48);

            var text = Encoding.ASCII.GetString(ImageSerialization.SaveWebpToBytes(image));

            Assert.DoesNotContain("EXIF", text);
            Assert.DoesNotContain("XMP ", text);
            Assert.DoesNotContain("ICCP", text);
        }

        [Fact]
        public void SaveWebpToBytes_has_no_effect_on_the_source_image()
        {
            using var image = Photo(64, 48);
            var before = image.GetPixel(10, 10);

            ImageSerialization.SaveWebpToBytes(image);

            Assert.Equal(before, image.GetPixel(10, 10));
            Assert.Equal(64, image.Width);
        }

        [Theory]
        [InlineData("a.webp", "image/webp")]
        [InlineData("t/12.WEBP", "image/webp")]
        [InlineData("a.jpg", "image/jpeg")]
        [InlineData("a.JPEG", "image/jpeg")]
        [InlineData("a.txt", null)]
        public void GetContentTypeFromFileName_names_the_image_types(string fileName, string? expected)
        {
            //Without a type the file leaves for the CDN with no Content-Type at all, and a browser
            //has to sniff it.
            Assert.Equal(expected, MediaHelpers.GetContentTypeFromFileName(fileName));
        }

        //A gradient with a little structure: a flat image compresses to almost nothing, and the
        //size comparison would then prove very little.
        private static RasterImage Photo(int width, int height)
        {
            var image = new RasterImage(width, height);
            image.Fill((x, y) => (byte)(((x * 3) + (y * 2) + ((x * y) % 29)) % 256));
            return image;
        }
    }
}
