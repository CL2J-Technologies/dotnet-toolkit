using cl2j.FileStorage.Extensions;
using Xunit;

namespace cl2j.FileStorage.Tests
{
    public sealed class BinaryExtensionsTests
    {
        private static readonly byte[] Payload = [0, 1, 2, 250, 255];

        private readonly InMemoryFileStorageProvider provider = new();

        [Fact]
        public async Task WriteBytesAsync_then_ReadBytesAsync_returns_the_same_bytes()
        {
            await provider.WriteBytesAsync("data.bin", Payload);

            Assert.Equal(Payload, await provider.ReadBytesAsync("data.bin"));
        }

        [Fact]
        public async Task WriteBytesAsync_passes_the_content_type_to_the_provider()
        {
            await provider.WriteBytesAsync("image.png", Payload, "image/png");

            Assert.Equal("image/png", provider.ContentTypes["image.png"]);
        }

        [Fact]
        public async Task WriteBytesAsync_without_a_content_type_passes_none()
        {
            await provider.WriteBytesAsync("data.bin", Payload);

            Assert.Null(provider.ContentTypes["data.bin"]);
        }

        [Fact]
        public async Task ReadBytesAsync_of_a_missing_file_returns_null()
        {
            Assert.Null(await provider.ReadBytesAsync("missing.bin"));
        }

        [Fact]
        public async Task ReadObjectAsync_returns_a_stream_holding_the_file()
        {
            await provider.WriteBytesAsync("data.bin", Payload);

            using var stream = await provider.ReadObjectAsync("data.bin");

            Assert.NotNull(stream);
            Assert.Equal(Payload.Length, stream.Length);
            stream.Seek(0, SeekOrigin.Begin);
            var read = new byte[Payload.Length];
            await stream.ReadExactlyAsync(read, TestContext.Current.CancellationToken);
            Assert.Equal(Payload, read);
        }

        [Fact]
        public async Task ReadObjectAsync_of_a_missing_file_returns_null()
        {
            Assert.Null(await provider.ReadObjectAsync("missing.bin"));
        }
    }
}
