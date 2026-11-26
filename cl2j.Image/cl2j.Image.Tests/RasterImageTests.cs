using SkiaSharp;
using Xunit;

namespace cl2j.Image.Tests
{
    /// <summary>
    ///     <see cref="RasterImage"/>: the guard on its dimensions, the copy it hands out, and the
    ///     swap of its pixels.
    /// </summary>
    public class RasterImageTests
    {
        [Theory]
        [InlineData(0, 10)]
        [InlineData(10, 0)]
        [InlineData(-1, 10)]
        [InlineData(10, -1)]
        public void An_image_needs_positive_dimensions(int width, int height)
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new RasterImage(width, height));

            Assert.Equal("width", exception.ParamName);
            Assert.Contains($"{width} x {height}", exception.Message);
        }

        [Fact]
        public void A_clone_has_the_same_pixels_and_state_and_is_independent_of_the_original()
        {
            using var original = new RasterImage(4, 3);
            original.Fill((x, y) => (byte)(x * 10 + y));
            original.Origin = SKEncodedOrigin.RightTop;
            original.HasMetadata = true;

            using var clone = original.Clone();
            original.Fill((_, _) => 200);

            Assert.Equal((4, 3), (clone.Width, clone.Height));
            Assert.Equal(SKEncodedOrigin.RightTop, clone.Origin);
            Assert.True(clone.HasMetadata);
            Assert.Equal(((byte)32, (byte)32, (byte)32), clone.GetPixel(3, 2));
            Assert.Equal(((byte)200, (byte)200, (byte)200), original.GetPixel(3, 2));
        }

        [Fact]
        public void Replacing_the_pixels_with_a_new_bitmap_swaps_them()
        {
            using var image = new RasterImage(4, 3);

            image.Replace(new SKBitmap(8, 6));

            Assert.Equal((8, 6), (image.Width, image.Height));
        }

        [Fact]
        public void Replacing_the_pixels_with_the_bitmap_the_image_already_holds_changes_nothing()
        {
            //The old pixels are disposed on a swap, so swapping a bitmap for itself would dispose
            //the image's own pixels.
            using var image = new RasterImage(4, 3);
            image.Fill((_, _) => 90);

            image.Replace(image.Bitmap);

            Assert.Equal((4, 3), (image.Width, image.Height));
            Assert.Equal(((byte)90, (byte)90, (byte)90), image.GetPixel(0, 0));
        }
    }
}
