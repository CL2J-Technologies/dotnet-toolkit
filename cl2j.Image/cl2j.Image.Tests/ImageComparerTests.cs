using Xunit;

namespace cl2j.Image.Tests
{
    /// <summary>
    /// `ImageComparer` is `ImageUtils.AreImagesIdentical` plus a debug trail: the two originals
    /// saved where a wrong verdict can be looked at.
    /// </summary>
    public sealed class ImageComparerTests : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "cl2j-image-tests-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }

        [Fact]
        public void AreImagesIdentical_is_false_when_an_image_is_missing()
        {
            using var image = Stripes(vertical: true);

            Assert.False(ImageComparer.AreImagesIdentical(null!, image, Settings()));
            Assert.False(ImageComparer.AreImagesIdentical(image, null!, Settings()));
        }

        [Fact]
        public void AreImagesIdentical_writes_nothing_without_a_debug_level()
        {
            using var image = Stripes(vertical: true);
            using var copy = Stripes(vertical: true);

            Assert.True(ImageComparer.AreImagesIdentical(image, copy, Settings(debugLevel: 0)));

            Assert.False(Directory.Exists(directory));
        }

        [Fact]
        public void AreImagesIdentical_saves_the_pair_under_Identical_at_a_debug_level()
        {
            using var image = Stripes(vertical: true);
            using var copy = Stripes(vertical: true);

            Assert.True(ImageComparer.AreImagesIdentical(image, copy, Settings(debugLevel: 1)));

            Assert.True(File.Exists(Path.Combine(directory, "Identical", "first.jpg")));
            Assert.True(File.Exists(Path.Combine(directory, "Identical", "second.jpg")));
            Assert.False(Directory.Exists(Path.Combine(directory, "Different")));
        }

        [Fact]
        public void AreImagesIdentical_saves_the_pair_under_Different_when_the_verdict_is_no()
        {
            using var image = Stripes(vertical: true);
            using var other = Stripes(vertical: false);

            Assert.False(ImageComparer.AreImagesIdentical(image, other, Settings(debugLevel: 1)));

            Assert.True(File.Exists(Path.Combine(directory, "Different", "first.jpg")));
            Assert.True(File.Exists(Path.Combine(directory, "Different", "second.jpg")));
            Assert.False(Directory.Exists(Path.Combine(directory, "Identical")));
        }

        [Fact]
        public void Compare_gives_the_mean_grey_difference_of_two_images_of_the_same_size()
        {
            using var dark = TestImages.Grey(10, 10, (x, y) => 100);
            using var lighter = TestImages.Grey(10, 10, (x, y) => 130);

            Assert.True(ImageComparer.Compare(dark, lighter, out var diff));
            Assert.Equal(30, diff);
        }

        [Fact]
        public void Compare_refuses_images_of_different_sizes()
        {
            using var small = new RasterImage(10, 10);
            using var large = new RasterImage(10, 20);

            Assert.False(ImageComparer.Compare(small, large, out var diff));
            Assert.Equal(0, diff);
        }

        [Fact]
        public void Equals_gives_the_share_of_pixels_within_the_tolerance()
        {
            // The left half is the same, the right half differs by 50 grey levels.
            using var reference = TestImages.Grey(10, 10, (x, y) => 100);
            using var edited = TestImages.Grey(10, 10, (x, y) => x < 5 ? (byte)100 : (byte)150);

            Assert.Equal(0.5, ImageComparer.Equals(reference, edited, pixelDiffMax: 19));
            Assert.Equal(1.0, ImageComparer.Equals(reference, edited, pixelDiffMax: 50));
        }

        [Fact]
        public void Equals_is_zero_for_images_of_different_sizes()
        {
            using var small = new RasterImage(10, 10);
            using var large = new RasterImage(20, 10);

            Assert.Equal(0, ImageComparer.Equals(small, large, pixelDiffMax: 255));
        }

        // --- Helpers -----------------------------------------------------------------------------

        private ImageComparer.ImageCompareSettings Settings(int debugLevel = 0) => new()
        {
            DebugLevel = debugLevel,
            SavePath = directory,
            Name1 = "first",
            Name2 = "second",
        };

        // Dark and mid grey bands, none near white, so no border is cropped; the two orientations
        // are as far apart as two images of the same size can be.
        private static RasterImage Stripes(bool vertical) =>
            TestImages.Grey(64, 64, (x, y) => ((vertical ? x : y) / 8) % 2 == 0 ? (byte)30 : (byte)180);
    }
}
