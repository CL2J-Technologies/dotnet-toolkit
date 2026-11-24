using System.Net;
using System.Net.Sockets;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using cl2j.FileStorage.Extensions;
using cl2j.FileStorage.Provider.S3;
using cl2j.Tooling.Exceptions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace cl2j.FileStorage.S3.IntegrationTests
{
    /// <summary>
    ///     The provider paths the contract and the behaviour tests leave open: configuration
    ///     validation, listing across pages and levels, metadata, content type and the streams
    ///     the provider is handed.
    /// </summary>
    [Collection(S3MockCollectionDefinition.Name)]
    public sealed class S3ProviderPathsTests(S3MockFixture s3mock)
    {
        private static string NewBucket() => "paths-" + Guid.NewGuid().ToString("N")[..12];

        private IConfigurationSection Section(string? bucket, string? accessKey = "test", string? secretKey = "test", bool createIfMissing = true, string? serviceUrl = null)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["S3:Bucket"] = bucket,
                    ["S3:AccessKey"] = accessKey,
                    ["S3:SecretKey"] = secretKey,
                    ["S3:ServiceUrl"] = serviceUrl ?? s3mock.ServiceUrl,
                    ["S3:CreateIfMissing"] = createIfMissing ? "true" : "false"
                })
                .Build()
                .GetSection("S3");
        }

        private FileStorageProviderS3 Provider(string bucket)
        {
            var provider = new FileStorageProviderS3();
            provider.Initialize("S3", Section(bucket));
            return provider;
        }

        // A client of the same service, to look at what the provider left there without going
        // through the provider.
        private AmazonS3Client RawClient() => new(
            new BasicAWSCredentials(S3MockFixture.AccessKey, S3MockFixture.SecretKey),
            new AmazonS3Config { ServiceURL = s3mock.ServiceUrl, ForcePathStyle = true });

        /// <summary>A stream that can be read once, front to back, like a network or a pipe.</summary>
        private sealed class ForwardOnlyStream(byte[] content) : Stream
        {
            private readonly MemoryStream inner = new(content);

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        #region Initialize

        [Fact]
        public void The_provider_name_is_kept()
        {
            var provider = new FileStorageProviderS3();
            provider.Initialize("my-store", Section(NewBucket()));

            Assert.Equal("my-store", provider.Name);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void A_provider_without_a_bucket_is_refused(string? bucket)
        {
            var ex = Assert.Throws<NotFoundException>(() => new FileStorageProviderS3().Initialize("S3", Section(bucket)));

            Assert.Contains("Bucket", ex.Message);
        }

        [Theory]
        [InlineData(null, "secret")]
        [InlineData("", "secret")]
        [InlineData("key", null)]
        [InlineData("key", "")]
        [InlineData(null, null)]
        public void A_provider_missing_either_credential_is_refused(string? accessKey, string? secretKey)
        {
            var ex = Assert.Throws<NotFoundException>(() => new FileStorageProviderS3().Initialize("S3", Section(NewBucket(), accessKey, secretKey)));

            Assert.Contains("AccessKey and SecretKey", ex.Message);
        }

        [Fact]
        public async Task An_existing_bucket_is_used_as_it_is_when_creation_is_off()
        {
            var bucket = NewBucket();
            await Provider(bucket).WriteTextAsync("kept.txt", "kept");

            var provider = new FileStorageProviderS3();
            provider.Initialize("S3", Section(bucket, createIfMissing: false));

            Assert.Equal("kept", await provider.ReadTextAsync("kept.txt"));
        }

        [Fact]
        public void Initializing_twice_on_a_bucket_that_is_there_creates_nothing_new()
        {
            var bucket = NewBucket();
            Provider(bucket);

            var ex = Record.Exception(() => Provider(bucket));

            Assert.Null(ex);
        }

        // A store that answers 403 for a bucket does not say whether it exists. Reading that as
        // "it exists" is how a typo gets past startup, so it has to stop here.
        [Fact]
        public async Task A_store_that_refuses_to_say_whether_the_bucket_exists_is_a_configuration_error()
        {
            using var listener = new HttpListener();
            var port = FreePort();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            listener.Start();
            var serving = Task.Run(async () =>
            {
                while (listener.IsListening)
                {
                    try
                    {
                        var context = await listener.GetContextAsync();
                        context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                        context.Response.Close();
                    }
                    catch (Exception) when (!listener.IsListening)
                    {
                        // Stopped while waiting for a request.
                    }
                }
            });

            try
            {
                var ex = await Assert.ThrowsAsync<BadRequestException>(() => Task.Run(() =>
                    new FileStorageProviderS3().Initialize("S3", Section(NewBucket(), serviceUrl: $"http://127.0.0.1:{port}"))));

                Assert.Contains("cannot reach bucket", ex.Message);
            }
            finally
            {
                listener.Stop();
                await serving.WaitAsync(TimeSpan.FromSeconds(5));
            }
        }

        private static int FreePort()
        {
            var tcp = new TcpListener(IPAddress.Loopback, 0);
            tcp.Start();
            var port = ((IPEndPoint)tcp.LocalEndpoint).Port;
            tcp.Stop();
            return port;
        }

        [Fact]
        public async Task A_provider_that_was_not_initialized_refuses_to_work()
        {
            var provider = new FileStorageProviderS3();

            await Assert.ThrowsAsync<BadRequestException>(() => provider.ExistsAsync("a.txt"));
            await Assert.ThrowsAsync<BadRequestException>(() => provider.ListFilesAsync("a"));
            await Assert.ThrowsAsync<BadRequestException>(() => provider.DeleteAsync("a.txt"));
        }

        #endregion Initialize

        #region Metadata

        [Fact]
        public async Task Info_reports_the_size_and_one_timestamp_for_all_three_dates()
        {
            var provider = Provider(NewBucket());
            var before = DateTime.UtcNow.AddMinutes(-5);
            await provider.WriteTextAsync("sized.txt", "twelve bytes");

            var info = await provider.GetInfoAsync("sized.txt");

            Assert.NotNull(info);
            Assert.Equal(12, info.Size);
            Assert.True(info.LastModified > before);
            Assert.True(info.LastModified < DateTime.UtcNow.AddMinutes(5));
            Assert.Equal(info.LastModified, info.Created);
            Assert.Equal(info.LastModified, info.CreatedOn);
        }

        [Fact]
        public async Task Overwriting_changes_the_size_reported()
        {
            var provider = Provider(NewBucket());
            await provider.WriteTextAsync("grows.txt", "ab");
            await provider.WriteTextAsync("grows.txt", "abcdef");

            Assert.Equal(6, (await provider.GetInfoAsync("grows.txt"))!.Size);
        }

        [Fact]
        public async Task A_name_exists_once_written_and_not_once_deleted()
        {
            var provider = Provider(NewBucket());
            await provider.WriteTextAsync("brief.txt", "x");
            Assert.True(await provider.ExistsAsync("brief.txt"));

            await provider.DeleteAsync("brief.txt");

            Assert.False(await provider.ExistsAsync("brief.txt"));
        }

        [Fact]
        public async Task Deleting_a_name_that_is_not_there_is_not_an_error()
        {
            var provider = Provider(NewBucket());

            var ex = await Record.ExceptionAsync(() => provider.DeleteAsync("never-was.txt"));

            Assert.Null(ex);
        }

        #endregion Metadata

        #region Listing

        // The service returns at most 1000 keys a response; a listing that stops at the first
        // page silently loses the rest.
        [Fact]
        public async Task A_listing_longer_than_one_page_comes_back_whole()
        {
            var provider = Provider(NewBucket());
            const int count = 1005;
            await Parallel.ForEachAsync(Enumerable.Range(0, count), new ParallelOptions { MaxDegreeOfParallelism = 16 },
                async (i, _) => await provider.WriteTextAsync($"many/f{i:D4}.txt", "x"));

            var files = (await provider.ListFilesAsync("many")).ToList();

            Assert.Equal(count, files.Count);
            Assert.Equal(count, files.Distinct().Count());
            Assert.Equal("f0000.txt", files.Min());
            Assert.Equal("f1004.txt", files.Max());
        }

        [Fact]
        public async Task A_listing_of_the_root_names_what_sits_at_the_top()
        {
            var provider = Provider(NewBucket());
            await provider.WriteTextAsync("top.txt", "1");
            await provider.WriteTextAsync("dir/inner.txt", "2");

            Assert.Equal(["top.txt"], await provider.ListFilesAsync(string.Empty));
            Assert.Equal(["dir"], await provider.ListFoldersAsync(string.Empty));
        }

        [Fact]
        public async Task Folders_nested_below_a_folder_are_listed_one_level_at_a_time()
        {
            var provider = Provider(NewBucket());
            await provider.WriteTextAsync("a/b/c/deep.txt", "1");
            await provider.WriteTextAsync("a/b/sibling/other.txt", "2");
            await provider.WriteTextAsync("a/top.txt", "3");

            Assert.Equal(["b"], await provider.ListFoldersAsync("a"));
            Assert.Equal(["c", "sibling"], (await provider.ListFoldersAsync("a/b/")).Order());
            Assert.Equal(["top.txt"], await provider.ListFilesAsync("a"));
            Assert.Empty(await provider.ListFilesAsync("a/b"));
            Assert.Equal(["deep.txt"], await provider.ListFilesAsync("a/b/c"));
        }

        // A key ending in the delimiter is how the console and some tools make a folder appear.
        // It is the folder, not a file in it.
        [Fact]
        public async Task An_explicit_folder_marker_is_not_listed_as_a_file()
        {
            var bucket = NewBucket();
            var provider = Provider(bucket);
            using (var raw = RawClient())
                await raw.PutObjectAsync(new PutObjectRequest { BucketName = bucket, Key = "marked/", ContentBody = string.Empty });
            await provider.WriteTextAsync("marked/real.txt", "r");

            Assert.Equal(["real.txt"], await provider.ListFilesAsync("marked"));
        }

        [Fact]
        public async Task A_listing_by_name_prefix_stays_at_one_level()
        {
            var provider = Provider(NewBucket());
            await provider.WriteTextAsync("logs/app-1.txt", "1");
            await provider.WriteTextAsync("logs/app-2.txt", "2");
            await provider.WriteTextAsync("logs/other.txt", "3");
            await provider.WriteTextAsync("logs/app-sub/app-3.txt", "4");

            var files = (await provider.ListFilesAsync("logs", "app-")).Order().ToList();

            Assert.Equal(["app-1.txt", "app-2.txt"], files);
        }

        [Fact]
        public async Task A_prefix_with_nothing_under_it_lists_empty()
        {
            var provider = Provider(NewBucket());

            Assert.Empty(await provider.ListFilesAsync("nothing/here"));
            Assert.Empty(await provider.ListFoldersAsync("nothing/here"));
        }

        #endregion Listing

        #region Write

        [Fact]
        public async Task The_content_type_given_is_the_one_stored()
        {
            var bucket = NewBucket();
            var provider = Provider(bucket);
            using var content = new MemoryStream("{}"u8.ToArray());

            await provider.WriteAsync("typed.json", content, "application/json");

            using var raw = RawClient();
            var metadata = await raw.GetObjectMetadataAsync(bucket, "typed.json");
            Assert.Equal("application/json", metadata.Headers.ContentType);
        }

        [Fact]
        public async Task Without_a_content_type_the_service_default_is_stored()
        {
            var bucket = NewBucket();
            var provider = Provider(bucket);
            using var content = new MemoryStream("raw"u8.ToArray());

            await provider.WriteAsync("untyped.bin", content);

            using var raw = RawClient();
            var metadata = await raw.GetObjectMetadataAsync(bucket, "untyped.bin");
            Assert.NotEqual("application/json", metadata.Headers.ContentType);
            Assert.False(string.IsNullOrEmpty(metadata.Headers.ContentType));
        }

        [Fact]
        public async Task A_forward_only_stream_is_written_whole()
        {
            var provider = Provider(NewBucket());
            using var content = new ForwardOnlyStream("streamed content"u8.ToArray());

            await provider.WriteAsync("streamed.txt", content, "text/plain");

            Assert.Equal("streamed content", await provider.ReadTextAsync("streamed.txt"));
        }

        // The SDK disposes what it is handed. The caller's stream must come through still open,
        // so the same content can go to a second provider.
        [Fact]
        public async Task A_seekable_stream_is_not_closed_by_the_write()
        {
            var provider = Provider(NewBucket());
            using var content = new MemoryStream("twice"u8.ToArray());

            await provider.WriteAsync("first.txt", content);
            await provider.WriteAsync("second.txt", content);

            Assert.True(content.CanRead);
            Assert.Equal("twice", await provider.ReadTextAsync("first.txt"));
            Assert.Equal("twice", await provider.ReadTextAsync("second.txt"));
        }

        [Fact]
        public async Task An_empty_stream_writes_an_empty_object()
        {
            var provider = Provider(NewBucket());
            using var content = new MemoryStream();

            await provider.WriteAsync("empty.txt", content);

            Assert.True(await provider.ExistsAsync("empty.txt"));
            Assert.Equal(0, (await provider.GetInfoAsync("empty.txt"))!.Size);
        }

        [Fact]
        public async Task Appending_a_forward_only_stream_adds_to_what_is_there()
        {
            var provider = Provider(NewBucket());
            await provider.WriteTextAsync("tail.txt", "head-");
            using var addition = new ForwardOnlyStream("tail"u8.ToArray());

            await provider.AppendAsync("tail.txt", addition);

            Assert.Equal("head-tail", await provider.ReadTextAsync("tail.txt"));
        }

        [Fact]
        public async Task Reading_into_a_stream_fills_it_with_the_content()
        {
            var provider = Provider(NewBucket());
            await provider.WriteTextAsync("read.txt", "content");
            using var target = new MemoryStream();

            Assert.True(await provider.ReadAsync("read.txt", target));

            Assert.Equal("content"u8.ToArray(), target.ToArray());
        }

        #endregion Write
    }

    /// <summary>
    ///     The wrapper that keeps the SDK from closing the caller's stream. It is internal and the
    ///     assembly does not expose it to tests, so it is built by reflection.
    /// </summary>
    public sealed class NonClosingStreamTests
    {
        private static Stream Wrap(Stream inner)
        {
            var type = typeof(FileStorageProviderS3).Assembly.GetType("cl2j.FileStorage.Provider.S3.NonClosingStream", throwOnError: true)!;
            return (Stream)Activator.CreateInstance(type, inner)!;
        }

        [Fact]
        public void The_capabilities_are_those_of_the_stream_underneath()
        {
            using var inner = new MemoryStream();
            using var wrapper = Wrap(inner);

            Assert.True(wrapper.CanRead);
            Assert.True(wrapper.CanSeek);
            Assert.True(wrapper.CanWrite);

            using var forward = Wrap(new ReadOnlyForwardStream());
            Assert.True(forward.CanRead);
            Assert.False(forward.CanSeek);
            Assert.False(forward.CanWrite);
        }

        [Fact]
        public void Length_position_seek_and_set_length_pass_through()
        {
            using var inner = new MemoryStream("0123456789"u8.ToArray());
            using var wrapper = Wrap(inner);

            Assert.Equal(10, wrapper.Length);

            wrapper.Position = 4;
            Assert.Equal(4, inner.Position);
            Assert.Equal(4, wrapper.Position);

            Assert.Equal(7, wrapper.Seek(3, SeekOrigin.Current));
            Assert.Equal(7, inner.Position);

            wrapper.SetLength(5);
            Assert.Equal(5, inner.Length);
        }

        [Fact]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1835", Justification = "The array overloads are the members under test.")]
        public async Task Every_read_overload_reads_from_the_stream_underneath()
        {
            using var inner = new MemoryStream("abcdefgh"u8.ToArray());
            using var wrapper = Wrap(inner);

            var sync = new byte[2];
            Assert.Equal(2, wrapper.Read(sync, 0, 2));
            Assert.Equal("ab"u8.ToArray(), sync);

            var array = new byte[2];
            Assert.Equal(2, await wrapper.ReadAsync(array, 0, 2, CancellationToken.None));
            Assert.Equal("cd"u8.ToArray(), array);

            var memory = new byte[2];
            Assert.Equal(2, await wrapper.ReadAsync(memory.AsMemory(), CancellationToken.None));
            Assert.Equal("ef"u8.ToArray(), memory);

            Assert.Equal(6, inner.Position);
        }

        [Fact]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1835", Justification = "The array overloads are the members under test.")]
        public async Task Every_write_overload_writes_to_the_stream_underneath()
        {
            using var inner = new MemoryStream();
            using var wrapper = Wrap(inner);

            wrapper.Write("ab"u8.ToArray(), 0, 2);
            await wrapper.WriteAsync("cd"u8.ToArray(), 0, 2, CancellationToken.None);
            await wrapper.WriteAsync("ef"u8.ToArray().AsMemory(), CancellationToken.None);

            Assert.Equal("abcdef"u8.ToArray(), inner.ToArray());
        }

        [Fact]
        public async Task Flushing_reaches_the_stream_underneath()
        {
            var inner = new FlushCountingStream();
            using var wrapper = Wrap(inner);

            wrapper.Flush();
            await wrapper.FlushAsync(CancellationToken.None);

            Assert.Equal(2, inner.Flushes);
        }

        [Fact]
        public async Task Disposing_the_wrapper_leaves_the_stream_underneath_open()
        {
            using var inner = new MemoryStream("data"u8.ToArray());

            Wrap(inner).Dispose();
            await Wrap(inner).DisposeAsync();

            Assert.True(inner.CanRead);
            Assert.Equal(4, inner.Length);
        }

        private sealed class ReadOnlyForwardStream : Stream
        {
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count) => 0;
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        private sealed class FlushCountingStream : MemoryStream
        {
            public int Flushes { get; private set; }

            public override void Flush() => Flushes++;

            public override Task FlushAsync(CancellationToken cancellationToken)
            {
                Flushes++;
                return Task.CompletedTask;
            }
        }
    }
}
