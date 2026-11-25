using Xunit;

namespace cl2j.Image.Tests
{
    /// <summary>`ImageResizer.ResizeIfOversize`: an image within both limits is left alone, an oversize one is scaled so that both dimensions fit.</summary>
    public class ImageResizerTests
    {
        [Theory]
        [InlineData(100, 80, 1000, 1000)] // landscape, smaller
        [InlineData(80, 100, 1000, 1000)] // portrait, smaller
        [InlineData(100, 100, 1000, 1000)] // square, smaller
        [InlineData(1000, 1000, 1000, 1000)] // square, exactly at the limit
        [InlineData(400, 300, 400, 300)] // landscape, exactly at both limits
        [InlineData(300, 400, 600, 400)] // portrait, within unequal limits
        public void An_image_within_both_limits_is_returned_as_the_same_instance(int width, int height, int maxW, int maxH)
        {
            using var image = new RasterImage(width, height);

            var result = ImageResizer.ResizeIfOversize(image, maxW, maxH);

            Assert.Same(image, result);
            Assert.Equal(width, result.Width);
            Assert.Equal(height, result.Height);
        }

        [Theory]
        [InlineData(4000, 2000, 1000, 1000, 1000, 500)] // landscape
        [InlineData(2000, 4000, 1000, 1000, 500, 1000)] // portrait
        [InlineData(3000, 3000, 1000, 1000, 1000, 1000)] // square
        [InlineData(2000, 1000, 500, 1000, 500, 250)] // the width limit binds
        [InlineData(2000, 1000, 1000, 200, 400, 200)] // the height limit binds on a landscape image
        [InlineData(1000, 2000, 400, 1000, 400, 800)] // the width limit binds on a portrait image
        public void An_oversize_image_is_scaled_so_that_both_dimensions_fit_and_the_aspect_ratio_is_kept(int width, int height, int maxW, int maxH, int expectedW, int expectedH)
        {
            using var image = new RasterImage(width, height);

            using var result = ImageResizer.ResizeIfOversize(image, maxW, maxH);

            Assert.NotSame(image, result);
            Assert.Equal(expectedW, result.Width);
            Assert.Equal(expectedH, result.Height);
        }

        [Fact]
        public void An_image_oversize_in_one_dimension_only_is_scaled_down()
        {
            using var image = new RasterImage(800, 100);

            using var result = ImageResizer.ResizeIfOversize(image, 400, 400);

            Assert.Equal(400, result.Width);
            Assert.Equal(50, result.Height);
        }

        [Fact]
        public void A_portrait_image_that_is_also_too_wide_is_scaled_to_fit_both_limits()
        {
            //The old rule scaled the height only: 900 x 1000 against 500 x 800 came out 720 x 800, over the width limit.
            using var image = new RasterImage(900, 1000);

            using var result = ImageResizer.ResizeIfOversize(image, 500, 800);

            Assert.True(result.Width <= 500);
            Assert.True(result.Height <= 800);
            Assert.Equal(500, result.Width);
            Assert.Equal(555, result.Height);
        }

        [Fact]
        public void A_scaled_image_never_collapses_to_zero_pixels()
        {
            using var image = new RasterImage(10000, 2);

            using var result = ImageResizer.ResizeIfOversize(image, 100, 100);

            Assert.Equal(100, result.Width);
            Assert.True(result.Height >= 1);
        }
    }
}
