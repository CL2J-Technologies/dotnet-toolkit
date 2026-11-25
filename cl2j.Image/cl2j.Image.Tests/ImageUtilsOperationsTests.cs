using cl2j.Tooling.Exceptions;
using Xunit;

namespace cl2j.Image.Tests
{
    /// <summary>
    /// The geometry and comparison operations of `ImageUtils`: thumbnails, cropping, optimisation
    /// and the grey-level comparison behind duplicate detection. Images are built grey, with a
    /// level that tells where each pixel came from, so a crop or a letterbox is checked by looking
    /// at pixels rather than only at dimensions.
    /// </summary>
    public class ImageUtilsOperationsTests
    {
        // --- OptimizeImage -----------------------------------------------------------------------

        [Fact]
        public void OptimizeImage_resizes_an_oversized_image_and_reports_the_new_size()
        {
            var bytes = TestImages.Jpeg(2000, 1000);
            var original = bytes;

            var result = ImageUtils.OptimizeImage(ref bytes, max: 500);

            Assert.NotNull(result);
            Assert.True(result!.Modified);
            Assert.Equal(500, result.Width);
            Assert.Equal(250, result.Height);
            Assert.NotSame(original, bytes);
            Assert.Equal(500, ImageUtils.Identify(bytes)!.Width);
        }

        [Fact]
        public void OptimizeImage_leaves_an_already_compliant_image_untouched()
        {
            var bytes = TestImages.Jpeg(320, 240);
            var original = bytes;

            var result = ImageUtils.OptimizeImage(ref bytes);

            Assert.False(result!.Modified);
            Assert.Equal(320, result.Width);
            Assert.Equal(240, result.Height);
            Assert.Same(original, bytes);
        }

        [Theory]
        [InlineData(1, 120, 60)]
        [InlineData(6, 60, 120)]
        public void OptimizeImage_reencodes_a_photo_that_carries_metadata_and_reports_the_upright_size(int orientation, int width, int height)
        {
            var bytes = TestImages.WithExif(TestImages.Jpeg(120, 60), orientation);
            var original = bytes;

            var result = ImageUtils.OptimizeImage(ref bytes);

            Assert.True(result!.Modified);
            Assert.Equal(width, result.Width);
            Assert.Equal(height, result.Height);
            Assert.NotSame(original, bytes);
            using var reread = ImageUtils.ReadImage(bytes)!;
            Assert.Equal(width, reread.Width);
            Assert.False(ExifUtils.Strip(reread), "the re-encoded image must carry no metadata");
        }

        [Fact]
        public void OptimizeImage_reencodes_a_format_outside_the_web_list()
        {
            var bytes = TestImages.Bmp(64, 48);

            var result = ImageUtils.OptimizeImage(ref bytes);

            Assert.True(result!.Modified);
            Assert.Equal("JPEG", ImageUtils.Identify(bytes)!.Format);
        }

        [Fact]
        public void OptimizeImage_returns_null_and_keeps_the_bytes_when_they_are_not_an_image()
        {
            byte[] bytes = [1, 2, 3, 4, 5];

            Assert.Null(ImageUtils.OptimizeImage(ref bytes));
            Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, bytes);
        }

        // --- CleanImage ---------------------------------------------------------------------------

        [Fact]
        public void CleanImage_returns_null_for_missing_bytes()
        {
            Assert.Null(ImageUtils.CleanImage((byte[])null!));
        }

        [Fact]
        public void CleanImage_on_a_RasterImage_returns_a_smaller_one_when_it_is_oversized()
        {
            using var image = new RasterImage(400, 200);

            using var cleaned = image.CleanImage(100, out var modified);

            Assert.True(modified);
            Assert.NotSame(image, cleaned);
            Assert.Equal(100, cleaned.Width);
            Assert.Equal(50, cleaned.Height);
        }

        [Fact]
        public void CleanImage_on_a_RasterImage_returns_the_same_instance_when_there_is_nothing_to_do()
        {
            using var image = ImageUtils.ReadImage(TestImages.Jpeg(40, 20))!;

            var cleaned = image.CleanImage(100, out var modified);

            Assert.False(modified);
            Assert.Same(image, cleaned);
        }

        [Fact]
        public void CleanImage_on_a_RasterImage_leaves_a_freshly_created_image_alone()
        {
            using var image = new RasterImage(40, 20);

            var cleaned = image.CleanImage(100, out var modified);

            Assert.False(modified);
            Assert.Same(image, cleaned);
        }

        [Fact]
        public void CleanImage_on_a_RasterImage_turns_a_sideways_photo_upright_in_place()
        {
            using var image = ImageUtils.ReadImage(TestImages.WithExif(TestImages.Jpeg(120, 60), orientation: 6))!;

            var cleaned = image.CleanImage(1280, out var modified);

            Assert.True(modified);
            Assert.Same(image, cleaned);
            Assert.Equal(60, image.Width);
            Assert.Equal(120, image.Height);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void IsWebFormat_is_true_for_jpeg_and_png_only_among_the_formats_the_decoder_reads(bool web)
        {
            var bytes = web ? TestImages.Png(16, 16) : TestImages.Bmp(16, 16);

            Assert.Equal(web, ImageUtils.IsWebFormat(bytes));
        }

        [Fact]
        public void ReadImage_says_when_the_pixels_cannot_be_decoded()
        {
            // A valid header over scrambled pixel data: the codec is created, the decode fails.
            var png = TestImages.Png(32, 32);
            for (var i = 60; i < png.Length - 20; ++i)
                png[i] ^= 0x5A;

            Assert.Null(ImageUtils.ReadImage(png, out var failure));
            Assert.StartsWith("decoding failed", failure);
        }

        [Fact]
        public void IsWebFormat_is_false_for_what_is_not_an_image()
        {
            Assert.False(ImageUtils.IsWebFormat([1, 2, 3, 4, 5]));
        }

        // --- Thumbnails --------------------------------------------------------------------------

        [Fact]
        public void CreateThumbnailCropped_from_bytes_gives_the_requested_size()
        {
            using var thumbnail = ImageUtils.CreateThumbnailCropped(TestImages.Jpeg(640, 480), 320, 240);

            Assert.Equal(320, thumbnail.Width);
            Assert.Equal(240, thumbnail.Height);
        }

        [Fact]
        public void CreateThumbnailCropped_from_bytes_refuses_what_is_not_an_image()
        {
            Assert.Throws<ValidationException>(() => ImageUtils.CreateThumbnailCropped([1, 2, 3, 4, 5], 100, 100));
        }

        [Fact]
        public void CreateThumbnailCropped_cuts_the_centre_of_a_wider_image()
        {
            // Grey level = column / 2, so the level of a pixel says which column it came from.
            using var source = TestImages.Grey(400, 100, (x, y) => (byte)(x / 2));

            using var thumbnail = ImageUtils.CreateThumbnailCropped(source, 100, 100);

            Assert.Equal(100, thumbnail.Width);
            Assert.Equal(100, thumbnail.Height);
            Assert.Equal(75, thumbnail.GetPixel(0, 50).R);   // source column 150..151
            Assert.Equal(124, thumbnail.GetPixel(99, 50).R); // source column 248..249
            Assert.Equal(400, source.Width);
        }

        [Fact]
        public void CreateThumbnailCropped_cuts_the_centre_of_a_taller_image()
        {
            using var source = TestImages.Grey(100, 400, (x, y) => (byte)(y / 2));

            using var thumbnail = ImageUtils.CreateThumbnailCropped(source, 100, 100);

            Assert.Equal(100, thumbnail.Width);
            Assert.Equal(100, thumbnail.Height);
            Assert.Equal(75, thumbnail.GetPixel(50, 0).R);
            Assert.Equal(124, thumbnail.GetPixel(50, 99).R);
        }

        [Fact]
        public void CreateThumbnailCropped_cuts_then_scales_down_when_the_crop_is_still_larger()
        {
            using var source = new RasterImage(400, 200);

            using var thumbnail = ImageUtils.CreateThumbnailCropped(source, 50, 50);

            Assert.Equal(50, thumbnail.Width);
            Assert.Equal(50, thumbnail.Height);
        }

        [Fact]
        public void CreateThumbnailCropped_scales_down_an_image_that_already_has_the_ratio()
        {
            using var source = new RasterImage(400, 200);

            using var thumbnail = ImageUtils.CreateThumbnailCropped(source, 100, 50);

            Assert.Equal(100, thumbnail.Width);
            Assert.Equal(50, thumbnail.Height);
        }

        [Fact]
        public void CreateThumbnail_centres_a_portrait_image_between_bars_of_the_background_colour()
        {
            using var source = TestImages.Grey(100, 200, (x, y) => 100);

            using var thumbnail = ImageUtils.CreateThumbnail(source, 200, 100, new RgbColor(255, 0, 0));

            Assert.Equal(200, thumbnail.Width);
            Assert.Equal(100, thumbnail.Height);
            Assert.Equal((255, 0, 0), thumbnail.GetPixel(10, 50));
            Assert.Equal((255, 0, 0), thumbnail.GetPixel(189, 50));
            Assert.Equal((100, 100, 100), thumbnail.GetPixel(100, 50));
        }

        [Fact]
        public void CreateThumbnail_centres_a_landscape_image_between_bars_of_the_background_colour()
        {
            using var source = TestImages.Grey(200, 100, (x, y) => 100);

            using var thumbnail = ImageUtils.CreateThumbnail(source, 100, 200, RgbColor.White);

            Assert.Equal(100, thumbnail.Width);
            Assert.Equal(200, thumbnail.Height);
            Assert.Equal((255, 255, 255), thumbnail.GetPixel(50, 10));
            Assert.Equal((255, 255, 255), thumbnail.GetPixel(50, 189));
            Assert.Equal((100, 100, 100), thumbnail.GetPixel(50, 100));
        }

        [Fact]
        public void CreateThumbnail_needs_no_bars_when_the_image_has_the_ratio()
        {
            using var source = new RasterImage(400, 200);

            using var thumbnail = ImageUtils.CreateThumbnail(source, 100, 50, RgbColor.White);

            Assert.Equal(100, thumbnail.Width);
            Assert.Equal(50, thumbnail.Height);
        }

        [Fact]
        public void CreateThumbnailWithRatio_keeps_the_ratio_of_the_image()
        {
            using var source = new RasterImage(400, 200);

            using var thumbnail = ImageUtils.CreateThumbnailWithRatio(source, 100);

            Assert.Equal(100, thumbnail.Width);
            Assert.Equal(50, thumbnail.Height);
        }

        // --- Cropping ----------------------------------------------------------------------------

        [Fact]
        public void Crop_removes_the_near_white_border_on_every_side()
        {
            // 10 columns white on each side, 20 rows white at the top and bottom; the content
            // grows by one grey level per column so its first pixel is recognisable.
            using var source = TestImages.Grey(100, 100, (x, y) => x < 10 || x >= 90 || y < 20 || y >= 80 ? (byte)255 : (byte)(100 + x - 10));

            using var cropped = ImageUtils.Crop(source);

            Assert.Equal(80, cropped.Width);
            Assert.Equal(60, cropped.Height);
            Assert.Equal(100, cropped.GetPixel(0, 0).R);
            Assert.Equal(179, cropped.GetPixel(79, 59).R);
        }

        [Fact]
        public void Crop_returns_the_same_instance_when_there_is_no_border()
        {
            using var source = TestImages.Grey(40, 30, (x, y) => 100);

            Assert.Same(source, ImageUtils.Crop(source));
        }

        [Fact]
        public void Crop_keeps_an_entirely_white_image_whole()
        {
            using var source = TestImages.Grey(40, 30, (x, y) => 255);

            Assert.Same(source, ImageUtils.Crop(source));
        }

        [Fact]
        public void Crop_removes_a_border_on_one_side_only()
        {
            using var source = TestImages.Grey(50, 40, (x, y) => y >= 30 ? (byte)255 : (byte)100);

            using var cropped = ImageUtils.Crop(source);

            Assert.Equal(50, cropped.Width);
            Assert.Equal(30, cropped.Height);
        }

        [Fact]
        public void CropCenter_cuts_the_requested_size_out_of_the_middle()
        {
            using var source = TestImages.Grey(100, 100, (x, y) => (byte)(x + y));

            using var cropped = ImageUtils.CropCenter(source, 40, 20, out var modified);

            Assert.True(modified);
            Assert.Equal(40, cropped.Width);
            Assert.Equal(20, cropped.Height);
            Assert.Equal(30 + 40, cropped.GetPixel(0, 0).R);
        }

        [Theory]
        [InlineData(100, 100)]  // exactly the size asked
        [InlineData(200, 50)]   // wider than the image
        [InlineData(50, 200)]   // taller than the image
        public void CropCenter_returns_the_same_instance_when_there_is_nothing_to_cut(int width, int height)
        {
            using var source = new RasterImage(100, 100);

            var result = ImageUtils.CropCenter(source, width, height, out var modified);

            Assert.False(modified);
            Assert.Same(source, result);
        }

        // --- Comparison --------------------------------------------------------------------------

        [Fact]
        public void AreImagesIdentical_is_false_when_an_image_is_missing()
        {
            using var image = new RasterImage(10, 10);

            Assert.False(ImageUtils.AreImagesIdentical(null!, image, new ImageUtils.ImageCompareSettings()));
            Assert.False(ImageUtils.AreImagesIdentical(image, null!, new ImageUtils.ImageCompareSettings()));
        }

        [Fact]
        public void AreImagesIdentical_is_false_when_the_ratios_differ()
        {
            using var square = TestImages.Grey(100, 100, (x, y) => 100);
            using var wide = TestImages.Grey(200, 100, (x, y) => 100);

            Assert.False(ImageUtils.AreImagesIdentical(square, wide, new ImageUtils.ImageCompareSettings()));
        }

        [Fact]
        public void AreImagesIdentical_finds_the_same_photo_whichever_side_is_larger()
        {
            using var large = TestImages.Grey(200, 100, (x, y) => (byte)(40 + x / 2));
            using var small = TestImages.Grey(100, 50, (x, y) => (byte)(40 + x));

            var settings = new ImageUtils.ImageCompareSettings();

            Assert.True(ImageUtils.AreImagesIdentical(large, small, settings));
            Assert.True(ImageUtils.AreImagesIdentical(small, large, settings));
            Assert.Equal(200, large.Width);
            Assert.Equal(100, small.Width);
        }

        [Fact]
        public void AreImagesIdentical_ignores_a_white_border_around_the_photo()
        {
            using var bare = TestImages.Grey(100, 100, (x, y) => (byte)(40 + x));
            using var framed = TestImages.Grey(120, 120, (x, y) => x < 10 || x >= 110 || y < 10 || y >= 110 ? (byte)255 : (byte)(40 + x - 10));

            Assert.True(ImageUtils.AreImagesIdentical(bare, framed, new ImageUtils.ImageCompareSettings()));
        }

        [Fact]
        public void AreImagesIdentical_scales_down_a_cropped_photo_to_the_size_of_a_smaller_one()
        {
            using var framed = TestImages.Grey(120, 120, (x, y) => x < 10 || x >= 110 || y < 10 || y >= 110 ? (byte)255 : (byte)(40 + x - 10));
            using var small = TestImages.Grey(50, 50, (x, y) => (byte)(40 + 2 * x));

            Assert.True(ImageUtils.AreImagesIdentical(small, framed, new ImageUtils.ImageCompareSettings()));
            Assert.True(ImageUtils.AreImagesIdentical(framed, small, new ImageUtils.ImageCompareSettings()));
        }

        [Fact]
        public void AreImagesIdentical_honours_the_thresholds_of_the_settings()
        {
            using var a = TestImages.Grey(40, 40, (x, y) => 100);
            using var b = TestImages.Grey(40, 40, (x, y) => 140);

            Assert.False(ImageUtils.AreImagesIdentical(a, b, new ImageUtils.ImageCompareSettings()));
            Assert.True(ImageUtils.AreImagesIdentical(a, b, new ImageUtils.ImageCompareSettings { PixelDifferenceTolerance = 40, CompareDifferenceMax = 40 }));
        }

        [Fact]
        public void ImageCompareSettings_default_to_the_thresholds_the_verdicts_were_tuned_with()
        {
            var settings = new ImageUtils.ImageCompareSettings();

            Assert.Equal(19, settings.PixelDifferenceTolerance);
            Assert.Equal(30, settings.CompareDifferenceMax);
            Assert.Equal(0.6, settings.PercentEqualsMin);
        }

        [Fact]
        public void Compare_gives_the_mean_grey_difference_of_two_images_of_the_same_size()
        {
            using var dark = TestImages.Grey(10, 10, (x, y) => 100);
            using var lighter = TestImages.Grey(10, 10, (x, y) => 110);

            Assert.True(ImageUtils.Compare(dark, lighter, out var diff));
            //The grey levels are truncated from a weighted sum, so one level of rounding is allowed.
            Assert.InRange(diff, 9, 10);
        }

        [Fact]
        public void Compare_refuses_images_of_different_sizes()
        {
            using var small = new RasterImage(10, 10);
            using var large = new RasterImage(10, 20);

            Assert.False(ImageUtils.Compare(small, large, out var diff));
            Assert.Equal(0, diff);
        }

        [Fact]
        public void Equals_counts_the_pixels_within_the_tolerance()
        {
            using var reference = TestImages.Grey(10, 10, (x, y) => 100);
            using var edited = TestImages.Grey(10, 10, (x, y) => y < 3 ? (byte)150 : (byte)105);

            Assert.Equal(0.7, ImageUtils.Equals(reference, edited, pixelDiffMax: 19), precision: 6);
        }

        [Fact]
        public void Equals_is_zero_for_images_of_different_sizes()
        {
            using var small = new RasterImage(10, 10);
            using var large = new RasterImage(20, 10);

            Assert.Equal(0.0, ImageUtils.Equals(small, large, pixelDiffMax: 255));
        }

        [Fact]
        public void GenerateDiffImage_holds_the_per_channel_difference()
        {
            using var a = TestImages.Grey(4, 4, (x, y) => 200);
            using var b = TestImages.Grey(4, 4, (x, y) => 150);

            using var diff = ImageUtils.GenerateDiffImage(a, b);

            Assert.Equal(4, diff.Width);
            Assert.Equal((50, 50, 50), diff.GetPixel(2, 2));
            Assert.Equal((50, 50, 50), ImageUtils.GenerateDiffImage(b, a).GetPixel(0, 0));
        }

        [Fact]
        public void GenerateDiffImage_refuses_images_of_different_sizes()
        {
            using var small = new RasterImage(4, 4);
            using var large = new RasterImage(4, 5);

            Assert.Throws<BadRequestException>(() => ImageUtils.GenerateDiffImage(small, large));
        }
    }
}
