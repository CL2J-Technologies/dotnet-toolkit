using cl2j.FileStorage.Core;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace cl2j.Image.Tests
{
    /// <summary>
    /// `MediaHelpers.UploadMediaAsync` is what puts a cleaned photo on the CDN. The overload that
    /// takes a `RasterImage` once serialized the original instead of the cleaned image, so the
    /// resize was computed and thrown away: these tests read back what the store received.
    /// </summary>
    public class MediaHelpersTests
    {
        [Fact]
        public async Task UploadMediaAsync_stores_the_cleaned_bytes_with_the_content_type_of_the_name()
        {
            var storage = new RecordingStorage();

            await MediaHelpers.UploadMediaAsync(storage, "photos/flat.jpg", TestImages.Jpeg(2000, 1000), max: 500);

            var written = Assert.Single(storage.Written);
            Assert.Equal("photos/flat.jpg", written.Name);
            Assert.Equal("image/jpeg", written.ContentType);
            Assert.Equal(500, ImageUtils.Identify(written.Bytes)!.Width);
        }

        [Fact]
        public async Task UploadMediaAsync_stores_an_already_compliant_image_as_it_came()
        {
            var storage = new RecordingStorage();
            var bytes = TestImages.Jpeg(320, 240);

            await MediaHelpers.UploadMediaAsync(storage, "flat.jpg", bytes);

            Assert.Equal(bytes, Assert.Single(storage.Written).Bytes);
        }

        [Fact]
        public async Task UploadMediaAsync_stores_nothing_when_there_are_no_bytes()
        {
            var storage = new RecordingStorage();

            await MediaHelpers.UploadMediaAsync(storage, "flat.jpg", (byte[])null!);

            Assert.Empty(storage.Written);
        }

        [Fact]
        public async Task UploadMediaAsync_stores_nothing_when_the_bytes_are_not_an_image()
        {
            var storage = new RecordingStorage();

            await Assert.ThrowsAnyAsync<Exception>(() => MediaHelpers.UploadMediaAsync(storage, "flat.jpg", [1, 2, 3, 4, 5]));

            Assert.Empty(storage.Written);
        }

        [Fact]
        public async Task UploadMediaAsync_for_an_image_stores_the_resized_one_and_leaves_the_original_usable()
        {
            var storage = new RecordingStorage();
            using var image = TestImages.Grey(800, 400, (x, y) => 100);

            await MediaHelpers.UploadMediaAsync(storage, "flat.jpg", image, max: 200);

            var written = Assert.Single(storage.Written);
            Assert.Equal("image/jpeg", written.ContentType);
            var header = ImageUtils.Identify(written.Bytes)!;
            Assert.Equal(200, header.Width);
            Assert.Equal(100, header.Height);
            Assert.Equal(800, image.Width);
            Assert.Equal((100, 100, 100), image.GetPixel(0, 0));
        }

        [Fact]
        public async Task UploadMediaAsync_for_an_image_already_small_enough_stores_it_without_disposing_it()
        {
            var storage = new RecordingStorage();
            using var image = TestImages.Grey(80, 40, (x, y) => 100);

            await MediaHelpers.UploadMediaAsync(storage, "flat.webp", image, max: 200);

            var written = Assert.Single(storage.Written);
            Assert.Equal("image/webp", written.ContentType);
            Assert.Equal(80, ImageUtils.Identify(written.Bytes)!.Width);
            Assert.Equal((100, 100, 100), image.GetPixel(0, 0));
        }

        [Theory]
        [InlineData("a.jpg", "image/jpeg")]
        [InlineData("a.JPG", "image/jpeg")]
        [InlineData("dir/a.jpeg", "image/jpeg")]
        [InlineData("a.webp", "image/webp")]
        [InlineData("a.WebP", "image/webp")]
        [InlineData("a.png", null)]
        [InlineData("a", null)]
        [InlineData("", null)]
        public void GetContentTypeFromFileName_maps_the_extension(string fileName, string? expected)
        {
            Assert.Equal(expected, MediaHelpers.GetContentTypeFromFileName(fileName));
        }

        private sealed record Write(string Name, byte[] Bytes, string? ContentType);

        private sealed class RecordingStorage : IFileStorageProvider
        {
            public List<Write> Written { get; } = [];

            public string Name { get; set; } = "recording";

            public async Task WriteAsync(string name, Stream stream, string? contentType = null)
            {
                using var copy = new MemoryStream();
                await stream.CopyToAsync(copy);
                Written.Add(new Write(name, copy.ToArray(), contentType));
            }

            public void Initialize(string providerName, IConfigurationSection configuration) => throw new NotSupportedException();
            public Task<bool> ExistsAsync(string name) => throw new NotSupportedException();
            public Task<FileStoreFileInfo?> GetInfoAsync(string name) => throw new NotSupportedException();
            public Task<IEnumerable<string>> ListFilesAsync(string path) => throw new NotSupportedException();
            public Task<IEnumerable<string>> ListFoldersAsync(string path) => throw new NotSupportedException();
            public Task<bool> ReadAsync(string name, Stream stream) => throw new NotSupportedException();
            public Task AppendAsync(string name, Stream stream) => throw new NotSupportedException();
            public Task DeleteAsync(string name) => throw new NotSupportedException();
        }
    }
}
