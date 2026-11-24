using cl2j.Tooling.Exceptions;
using Xunit;

namespace cl2j.Image.Tests
{
    /// <summary>The edges of `ImageUtils` the main suites do not reach: degenerate crops and comparison thresholds.</summary>
    public class ImageUtilsEdgeTests
    {
        [Theory]
        [InlineData(0, 0)]
        [InlineData(-10, -10)]
        public void CropCenter_refuses_a_size_that_cannot_be_cut_out(int width, int height)
        {
            using var source = new RasterImage(100, 100);

            Assert.Throws<BadRequestException>(() => ImageUtils.CropCenter(source, width, height, out _));
        }

        [Fact]
        public void AreImagesIdentical_asks_for_the_share_of_equal_pixels_the_settings_require()
        {
            using var first = TestImages.Grey(100, 100, (x, y) => (byte)(40 + x));
            using var second = TestImages.Grey(100, 100, (x, y) => (byte)(40 + x));

            Assert.True(ImageUtils.AreImagesIdentical(first, second, new ImageUtils.ImageCompareSettings()));
            //No image can have more than all of its pixels equal.
            Assert.False(ImageUtils.AreImagesIdentical(first, second, new ImageUtils.ImageCompareSettings { PercentEqualsMin = 1.1 }));
        }
    }
}
