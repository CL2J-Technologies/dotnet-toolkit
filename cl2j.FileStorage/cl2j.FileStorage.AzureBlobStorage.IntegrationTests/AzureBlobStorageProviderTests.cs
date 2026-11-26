using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using cl2j.FileStorage.Core;
using cl2j.FileStorage.Extensions;
using cl2j.FileStorage.Provider.AzureBlobStorage;
using cl2j.FileStorage.Tests;
using cl2j.Tooling.Exceptions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace cl2j.FileStorage.AzureBlobStorage.IntegrationTests
{
    internal static class AzureProviderFactory
    {
        public static string NewContainerName(string prefix) => prefix + "-" + Guid.NewGuid().ToString("N")[..12];

        /// <summary>
        ///     Configured the way a consumer does it: a configuration section with the documented keys.
        /// </summary>
        public static FileStorageProviderAzureBlobStorage Create(string connectionString, string? container, bool? createIfMissing)
        {
            var settings = new Dictionary<string, string?>
            {
                ["Azure:ConnectionString"] = connectionString
            };
            if (container != null)
                settings["Azure:Container"] = container;
            if (createIfMissing != null)
                settings["Azure:CreateIfMissing"] = createIfMissing.Value ? "true" : "false";

            var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

            var provider = new FileStorageProviderAzureBlobStorage();
            provider.Initialize("Azure", configuration.GetSection("Azure"));
            return provider;
        }
    }

    /// <summary>
    ///     The contract, run against a real Azure Blob implementation (Azurite).
    ///
    ///     <para>
    ///     A container of its own per class instance, created on initialize: the contract writes
    ///     at fixed names, so sharing one between runs would let a leftover blob decide a later
    ///     result.
    ///     </para>
    /// </summary>
    [Collection(AzuriteCollectionDefinition.Name)]
    public sealed class AzureBlobStorageProviderContractTests : FileStorageProviderContract
    {
        private readonly FileStorageProviderAzureBlobStorage provider;

        public AzureBlobStorageProviderContractTests(AzuriteFixture azurite)
        {
            provider = AzureProviderFactory.Create(azurite.ConnectionString, AzureProviderFactory.NewContainerName("contract"), createIfMissing: true);
        }

        protected override IFileStorageProvider Provider => provider;
    }

    /// <summary>
    ///     What the contract does not cover, because it is specific to Azure Blob Storage.
    /// </summary>
    [Collection(AzuriteCollectionDefinition.Name)]
    public sealed class AzureBlobStorageProviderBehaviourTests(AzuriteFixture azurite)
    {
        private FileStorageProviderAzureBlobStorage Provider(string? container = null) =>
            AzureProviderFactory.Create(azurite.ConnectionString, container ?? AzureProviderFactory.NewContainerName("behaviour"), createIfMissing: true);

        private BlobContainerClient Client(string container) => new(azurite.ConnectionString, container);

        private static IEnumerable<string> Sorted(IEnumerable<string> names) => names.OrderBy(n => n, StringComparer.Ordinal);

        // ---- Container configuration ----

        [Fact]
        public void A_missing_container_is_refused_when_creation_is_not_asked_for()
        {
            var name = AzureProviderFactory.NewContainerName("absent");

            var ex = Assert.Throws<NotFoundException>(() => AzureProviderFactory.Create(azurite.ConnectionString, name, createIfMissing: false));

            Assert.Contains("does not exist", ex.Message);
            Assert.Contains(name, ex.Message);
            Assert.False(Client(name).Exists(TestContext.Current.CancellationToken), "a refused container must not have been created on the way");
        }

        [Fact]
        public void A_missing_container_is_refused_when_CreateIfMissing_is_not_configured_at_all()
        {
            // The default is off: a name spelled wrong must fail, not create a container.
            var name = AzureProviderFactory.NewContainerName("default");

            Assert.Throws<NotFoundException>(() => AzureProviderFactory.Create(azurite.ConnectionString, name, createIfMissing: null));

            Assert.False(Client(name).Exists(TestContext.Current.CancellationToken));
        }

        [Fact]
        public void A_missing_container_is_created_when_asked_for()
        {
            var name = AzureProviderFactory.NewContainerName("created");

            AzureProviderFactory.Create(azurite.ConnectionString, name, createIfMissing: true);

            Assert.True(Client(name).Exists(TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task An_existing_container_is_used_as_it_is_whatever_CreateIfMissing_says()
        {
            var name = AzureProviderFactory.NewContainerName("existing");
            await Client(name).CreateAsync(cancellationToken: TestContext.Current.CancellationToken);
            await Client(name).GetBlobClient("already.txt").UploadAsync(new MemoryStream("there"u8.ToArray()), TestContext.Current.CancellationToken);

            var withoutCreation = AzureProviderFactory.Create(azurite.ConnectionString, name, createIfMissing: false);
            var withCreation = AzureProviderFactory.Create(azurite.ConnectionString, name, createIfMissing: true);

            Assert.Equal("there", await withoutCreation.ReadTextAsync("already.txt"));
            Assert.Equal("there", await withCreation.ReadTextAsync("already.txt"));
        }

        [Fact]
        public void No_container_name_is_a_configuration_error()
        {
            var ex = Assert.Throws<NotFoundException>(() => AzureProviderFactory.Create(azurite.ConnectionString, container: null, createIfMissing: true));

            Assert.Contains("Container", ex.Message);
        }

        [Fact]
        public void The_provider_keeps_the_name_it_was_initialized_with()
        {
            Assert.Equal("Azure", Provider().Name);
        }

        [Fact]
        public async Task A_provider_that_was_never_initialized_refuses_to_work()
        {
            var provider = new FileStorageProviderAzureBlobStorage();

            await Assert.ThrowsAsync<BadRequestException>(() => provider.ExistsAsync("a.txt"));
            await Assert.ThrowsAsync<BadRequestException>(() => provider.ReadAsync("a.txt", new MemoryStream()));
            await Assert.ThrowsAsync<BadRequestException>(() => provider.DeleteAsync("a.txt"));
            await Assert.ThrowsAsync<BadRequestException>(() => provider.GetInfoAsync("a.txt"));
            await Assert.ThrowsAsync<BadRequestException>(() => provider.WriteAsync("a.txt", new MemoryStream(), null));
            await Assert.ThrowsAsync<BadRequestException>(() => provider.ListFilesAsync("folder"));
            await Assert.ThrowsAsync<BadRequestException>(() => provider.ListFilesAsync("folder", "prefix"));
            await Assert.ThrowsAsync<BadRequestException>(() => provider.ListFoldersAsync("folder"));
        }

        // ---- A name that is not there ----

        [Fact]
        public async Task Asking_about_a_name_that_is_not_there_is_not_an_error()
        {
            var provider = Provider();

            Assert.False(await provider.ExistsAsync("absent.txt"));
            Assert.Null(await provider.GetInfoAsync("absent.txt"));
            Assert.False(await provider.ReadAsync("absent.txt", new MemoryStream()));
        }

        [Fact]
        public async Task Reading_a_name_that_is_not_there_leaves_the_stream_untouched()
        {
            var provider = Provider();
            using var target = new MemoryStream();

            await provider.ReadAsync("absent.txt", target);

            Assert.Equal(0, target.Length);
        }

        [Fact]
        public async Task Deleting_a_name_that_is_not_there_is_reported_by_the_service()
        {
            // The provider does not swallow it: the service's answer reaches the caller.
            var provider = Provider();

            var ex = await Assert.ThrowsAsync<RequestFailedException>(() => provider.DeleteAsync("absent.txt"));

            Assert.Equal(404, ex.Status);
        }

        // ---- Write, read, content type ----

        [Fact]
        public async Task Bytes_survive_a_round_trip_unchanged()
        {
            var provider = Provider();
            var bytes = Enumerable.Range(0, 1024).Select(i => (byte)(i % 256)).ToArray();

            using (var content = new MemoryStream(bytes))
                await provider.WriteAsync("binary.bin", content, "application/octet-stream");

            using var read = new MemoryStream();
            Assert.True(await provider.ReadAsync("binary.bin", read));
            Assert.Equal(bytes, read.ToArray());
        }

        [Fact]
        public async Task An_empty_file_can_be_written_and_read()
        {
            var provider = Provider();

            await provider.WriteTextAsync("empty.txt", string.Empty);

            Assert.True(await provider.ExistsAsync("empty.txt"));
            Assert.Equal(string.Empty, await provider.ReadTextAsync("empty.txt"));
            Assert.Equal(0, (await provider.GetInfoAsync("empty.txt"))!.Size);
        }

        [Fact]
        public async Task The_content_type_given_on_write_is_stored()
        {
            var container = AzureProviderFactory.NewContainerName("ctype");
            var provider = Provider(container);

            using var content = new MemoryStream("{}"u8.ToArray());
            await provider.WriteAsync("data.json", content, "application/json");

            var properties = (await Client(container).GetBlobClient("data.json").GetPropertiesAsync(cancellationToken: TestContext.Current.CancellationToken)).Value;
            Assert.Equal("application/json", properties.ContentType);
        }

        [Fact]
        public async Task Without_a_content_type_the_service_default_applies()
        {
            var container = AzureProviderFactory.NewContainerName("noctype");
            var provider = Provider(container);

            using var content = new MemoryStream("x"u8.ToArray());
            await provider.WriteAsync("untyped.bin", content, null);

            var properties = (await Client(container).GetBlobClient("untyped.bin").GetPropertiesAsync(cancellationToken: TestContext.Current.CancellationToken)).Value;
            Assert.Equal("application/octet-stream", properties.ContentType);
        }

        [Fact]
        public async Task Writing_over_a_file_replaces_its_content_type()
        {
            var container = AzureProviderFactory.NewContainerName("retype");
            var provider = Provider(container);

            using (var first = new MemoryStream("a"u8.ToArray()))
                await provider.WriteAsync("f.txt", first, "text/plain");
            using (var second = new MemoryStream("<a/>"u8.ToArray()))
                await provider.WriteAsync("f.txt", second, "application/xml");

            var properties = (await Client(container).GetBlobClient("f.txt").GetPropertiesAsync(cancellationToken: TestContext.Current.CancellationToken)).Value;
            Assert.Equal("application/xml", properties.ContentType);
        }

        // The caller may hand the same stream to two providers; a caller should not have to know
        // which one moved it.
        [Fact]
        public async Task A_written_stream_is_left_rewound()
        {
            var provider = Provider();
            using var content = new MemoryStream("payload"u8.ToArray());

            await provider.WriteAsync("rewound.txt", content, "text/plain");

            Assert.Equal(0, content.Position);
        }

        // ---- Append ----

        [Fact]
        public async Task Appending_three_times_to_a_new_name_keeps_every_part_in_order()
        {
            var provider = Provider();

            using (var first = new MemoryStream("one\n"u8.ToArray()))
                await provider.AppendAsync("log.txt", first);
            using (var second = new MemoryStream("two\n"u8.ToArray()))
                await provider.AppendAsync("log.txt", second);
            using (var third = new MemoryStream("three\n"u8.ToArray()))
                await provider.AppendAsync("log.txt", third);

            Assert.Equal("one\ntwo\nthree\n", await provider.ReadTextAsync("log.txt"));
        }

        [Fact]
        public async Task Appending_creates_an_append_blob()
        {
            var container = AzureProviderFactory.NewContainerName("append");
            var provider = Provider(container);

            using var content = new MemoryStream("x"u8.ToArray());
            await provider.AppendAsync("a.log", content);

            var properties = (await Client(container).GetBlobClient("a.log").GetPropertiesAsync(cancellationToken: TestContext.Current.CancellationToken)).Value;
            Assert.Equal(BlobType.Append, properties.BlobType);
        }

        [Fact]
        public async Task Appending_to_a_file_written_with_write_is_refused_by_the_service()
        {
            // A file made by WriteAsync is a block blob, and only append blobs take blocks added to the end.
            var provider = Provider();
            await provider.WriteTextAsync("block.txt", "written");

            using var content = new MemoryStream("more"u8.ToArray());
            var ex = await Assert.ThrowsAsync<RequestFailedException>(() => provider.AppendAsync("block.txt", content));

            Assert.Equal("InvalidBlobType", ex.ErrorCode);
            Assert.Equal("written", await provider.ReadTextAsync("block.txt"));
        }

        // ---- Exists and info ----

        [Fact]
        public async Task A_container_deleted_after_the_provider_started_has_no_files()
        {
            var name = AzureProviderFactory.NewContainerName("gone");
            var provider = Provider(name);
            await Client(name).DeleteAsync(cancellationToken: TestContext.Current.CancellationToken);

            Assert.False(await provider.ExistsAsync("anything.txt"));
        }

        [Fact]
        public async Task A_file_that_was_written_exists()
        {
            var provider = Provider();
            await provider.WriteTextAsync("here.txt", "x");

            Assert.True(await provider.ExistsAsync("here.txt"));
        }

        [Fact]
        public async Task The_info_of_a_file_gives_its_size_and_dates()
        {
            var provider = Provider();
            var before = DateTime.UtcNow.AddMinutes(-5);
            await provider.WriteTextAsync("info.txt", "twelve bytes");

            var info = await provider.GetInfoAsync("info.txt");

            Assert.NotNull(info);
            Assert.Equal(12, info.Size);
            Assert.True(info.LastModified > before, "LastModified should be recent");
            Assert.True(info.LastModified < DateTime.UtcNow.AddMinutes(5));
            Assert.True(info.CreatedOn > before);
            Assert.Equal(info.CreatedOn, info.Created);
        }

        [Fact]
        public async Task The_size_in_the_info_follows_appends()
        {
            var provider = Provider();
            await provider.AppendTextAsync("grow.log", "abc");
            await provider.AppendTextAsync("grow.log", "defg");

            Assert.Equal(7, (await provider.GetInfoAsync("grow.log"))!.Size);
        }

        [Fact]
        public async Task A_deleted_file_has_no_info()
        {
            var provider = Provider();
            await provider.WriteTextAsync("gone.txt", "x");
            await provider.DeleteAsync("gone.txt");

            Assert.Null(await provider.GetInfoAsync("gone.txt"));
            Assert.False(await provider.ReadAsync("gone.txt", new MemoryStream()));
        }

        [Fact]
        public async Task Deleting_one_file_leaves_its_neighbours()
        {
            var provider = Provider();
            await provider.WriteTextAsync("dir/a.txt", "a");
            await provider.WriteTextAsync("dir/b.txt", "b");

            await provider.DeleteAsync("dir/a.txt");

            Assert.False(await provider.ExistsAsync("dir/a.txt"));
            Assert.True(await provider.ExistsAsync("dir/b.txt"));
        }

        [Fact]
        public async Task Two_containers_do_not_see_each_others_files()
        {
            var first = Provider();
            var second = Provider();
            await first.WriteTextAsync("only-in-first.txt", "x");

            Assert.True(await first.ExistsAsync("only-in-first.txt"));
            Assert.False(await second.ExistsAsync("only-in-first.txt"));
        }

        // ---- Listing ----

        [Fact]
        public async Task Listing_a_folder_returns_what_sits_directly_under_it()
        {
            var provider = Provider();
            await provider.WriteTextAsync("photos/one.txt", "1");
            await provider.WriteTextAsync("photos/two.txt", "2");
            await provider.WriteTextAsync("photos/deeper/three.txt", "3");
            await provider.WriteTextAsync("elsewhere/four.txt", "4");

            Assert.Equal(["one.txt", "two.txt"], Sorted(await provider.ListFilesAsync("photos")));
        }

        [Fact]
        public async Task Listing_a_nested_folder_names_only_its_own_files()
        {
            var provider = Provider();
            await provider.WriteTextAsync("a/b/c/leaf.txt", "1");
            await provider.WriteTextAsync("a/b/c/other.txt", "2");
            await provider.WriteTextAsync("a/b/top.txt", "3");

            Assert.Equal(["leaf.txt", "other.txt"], Sorted(await provider.ListFilesAsync("a/b/c")));
            Assert.Equal(["top.txt"], await provider.ListFilesAsync("a/b"));
        }

        [Fact]
        public async Task Listing_the_root_with_a_null_or_empty_path_names_the_files_at_the_root_only()
        {
            var provider = Provider();
            await provider.WriteTextAsync("top.txt", "1");
            await provider.WriteTextAsync("folder/inside.txt", "2");

            Assert.Equal(["top.txt"], await provider.ListFilesAsync(string.Empty));
            Assert.Equal(["top.txt"], await provider.ListFilesAsync(null!));
        }

        [Fact]
        public async Task Listing_a_folder_that_does_not_exist_is_empty()
        {
            var provider = Provider();
            await provider.WriteTextAsync("real/file.txt", "1");

            Assert.Empty(await provider.ListFilesAsync("imaginary"));
            Assert.Empty(await provider.ListFoldersAsync("imaginary"));
            Assert.Empty(await provider.ListFilesAsync("imaginary", "x"));
        }

        [Fact]
        public async Task Listing_by_name_prefix_in_a_nested_folder_and_with_a_trailing_separator()
        {
            var provider = Provider();
            await provider.WriteTextAsync("x/y/log-1.txt", "1");
            await provider.WriteTextAsync("x/y/log-2.txt", "2");
            await provider.WriteTextAsync("x/y/other.txt", "3");
            await provider.WriteTextAsync("x/log-up.txt", "4");

            Assert.Equal(["log-1.txt", "log-2.txt"], Sorted(await provider.ListFilesAsync("x/y", "log")));
            Assert.Equal(["log-1.txt", "log-2.txt"], Sorted(await provider.ListFilesAsync("x/y/", "log")));
        }

        [Fact]
        public async Task Listing_by_name_prefix_at_the_root_leaves_out_what_is_in_folders()
        {
            var provider = Provider();
            await provider.WriteTextAsync("ab.txt", "1");
            await provider.WriteTextAsync("ab/deep.txt", "2");

            Assert.Equal(["ab.txt"], await provider.ListFilesAsync(string.Empty, "ab"));
        }

        [Fact]
        public async Task Listing_by_a_name_prefix_that_matches_nothing_is_empty()
        {
            var provider = Provider();
            await provider.WriteTextAsync("dir/a.txt", "1");

            Assert.Empty(await provider.ListFilesAsync("dir", "zzz"));
        }

        [Fact]
        public async Task Listing_folders_names_the_sub_folders_once_each()
        {
            var provider = Provider();
            await provider.WriteTextAsync("root/first/a.txt", "a");
            await provider.WriteTextAsync("root/first/b.txt", "b");
            await provider.WriteTextAsync("root/first/deeper/c.txt", "c");
            await provider.WriteTextAsync("root/second/d.txt", "d");
            await provider.WriteTextAsync("root/e.txt", "e");

            Assert.Equal(["first", "second"], Sorted(await provider.ListFoldersAsync("root")));
        }

        [Fact]
        public async Task Listing_folders_of_a_nested_path_names_the_level_below()
        {
            var provider = Provider();
            await provider.WriteTextAsync("a/b/one/f.txt", "1");
            await provider.WriteTextAsync("a/b/two/g.txt", "2");
            await provider.WriteTextAsync("a/other/h.txt", "3");

            Assert.Equal(["one", "two"], Sorted(await provider.ListFoldersAsync("a/b")));
            Assert.Equal(["b", "other"], Sorted(await provider.ListFoldersAsync("a")));
        }

        [Fact]
        public async Task Listing_folders_ignores_files_directly_in_the_path()
        {
            var provider = Provider();
            await provider.WriteTextAsync("only/file.txt", "1");

            Assert.Empty(await provider.ListFoldersAsync("only"));
        }

        [Fact]
        public async Task Listing_files_handles_more_blobs_than_one_request_holds()
        {
            // The service pages listings; the provider has to walk every page, not stop at the first.
            var provider = Provider();
            await Task.WhenAll(Enumerable.Range(0, 60).Select(i => provider.WriteTextAsync($"many/f{i:D3}.txt", "x")));

            Assert.Equal(60, (await provider.ListFilesAsync("many")).Count());
            Assert.Equal(60, (await provider.ListFilesAsync("many", "f")).Count());
        }

        // ---- Listing boundaries (formerly known defects) ----

        [Fact]
        public async Task Listing_folders_at_the_root_names_the_top_level_folders()
        {
            var provider = Provider();
            await provider.WriteTextAsync("first/a.txt", "a");
            await provider.WriteTextAsync("second/b.txt", "b");

            Assert.Equal(["first", "second"], Sorted(await provider.ListFoldersAsync(string.Empty)));
        }

        [Fact]
        public async Task Listing_a_folder_does_not_include_files_of_a_sibling_whose_name_starts_the_same()
        {
            var provider = Provider();
            await provider.WriteTextAsync("photos/in.txt", "1");
            await provider.WriteTextAsync("photos-old.txt", "2");

            Assert.Equal(["in.txt"], await provider.ListFilesAsync("photos"));
        }

        [Fact]
        public async Task Listing_folders_does_not_include_the_sub_folders_of_a_sibling_whose_name_starts_the_same()
        {
            var provider = Provider();
            await provider.WriteTextAsync("photos/mine/a.txt", "1");
            await provider.WriteTextAsync("photos22/theirs/b.txt", "2");

            Assert.Equal(["mine"], await provider.ListFoldersAsync("photos"));
        }
    }
}
