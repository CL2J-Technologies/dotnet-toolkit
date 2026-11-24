using cl2j.FileStorage.Core;
using cl2j.Tooling.Exceptions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace cl2j.FileStorage.Tests
{
    /// <summary>
    ///     The factory keeps its providers in a static dictionary, shared by every instance. Each
    ///     test therefore names its providers with a fresh suffix so none sees another's.
    /// </summary>
    public sealed class FileStorageFactoryTests
    {
        private sealed class StubProvider : IFileStorageProvider
        {
            public string Name { get; set; } = null!;

            public IConfigurationSection? Configuration { get; private set; }

            public void Initialize(string providerName, IConfigurationSection configuration)
            {
                Name = providerName;
                Configuration = configuration;
            }

            public Task<bool> ExistsAsync(string name) => Task.FromResult(false);

            public Task<FileStoreFileInfo?> GetInfoAsync(string name) => Task.FromResult<FileStoreFileInfo?>(null);

            public Task<IEnumerable<string>> ListFilesAsync(string path) => Task.FromResult(Enumerable.Empty<string>());

            public Task<IEnumerable<string>> ListFoldersAsync(string path) => Task.FromResult(Enumerable.Empty<string>());

            public Task<bool> ReadAsync(string name, Stream stream) => Task.FromResult(false);

            public Task WriteAsync(string name, Stream stream, string? contentType = null) => Task.CompletedTask;

            public Task AppendAsync(string name, Stream stream) => Task.CompletedTask;

            public Task DeleteAsync(string name) => Task.CompletedTask;
        }

        private static readonly string Suffix = Guid.NewGuid().ToString("N");

        private static FileStorageFactory Build(params (string Name, string Type)[] storages)
        {
            var values = new Dictionary<string, string?>();
            foreach (var (name, type) in storages)
            {
                values[$"cl2j:FileStorage:Storages:{name}:Type"] = type;
                values[$"cl2j:FileStorage:Storages:{name}:Extra"] = "value-of-" + name;
            }

            return new FileStorageFactory(new ConfigurationBuilder().AddInMemoryCollection(values).Build());
        }

        [Fact]
        public void Register_creates_and_initializes_a_provider_for_each_configured_storage_of_the_type()
        {
            var name = "registered-" + Suffix;
            var factory = Build((name, "Stub"));

            factory.Register<StubProvider>("Stub");

            var provider = Assert.IsType<StubProvider>(factory.GetProvider(name));
            Assert.Equal(name, provider.Name);
            Assert.Equal("value-of-" + name, provider.Configuration!["Extra"]);
        }

        [Fact]
        public void Register_ignores_the_storages_of_another_type()
        {
            var name = "other-type-" + Suffix;
            var factory = Build((name, "SomethingElse"));

            factory.Register<StubProvider>("Stub");

            Assert.Empty(factory.Registrations);
            Assert.Throws<NotFoundException>(() => factory.GetProvider(name));
        }

        [Fact]
        public void Registrations_describes_each_provider_registered()
        {
            var name = "described-" + Suffix;
            var factory = Build((name, "Stub"));

            factory.Register<StubProvider>("Stub");

            Assert.Equal([$"'{name}' with provider 'StubProvider'"], factory.Registrations);
        }

        [Fact]
        public void Registering_a_name_twice_keeps_the_first_provider()
        {
            var name = "twice-" + Suffix;
            var factory = Build((name, "Stub"));

            factory.Register<StubProvider>("Stub");
            var first = factory.GetProvider(name);
            factory.Register<StubProvider>("Stub");

            Assert.Same(first, factory.GetProvider(name));
            Assert.Single(factory.Registrations);
        }

        [Fact]
        public void A_provider_registered_by_one_factory_is_found_by_another()
        {
            var name = "shared-" + Suffix;
            Build((name, "Stub")).Register<StubProvider>("Stub");

            Assert.NotNull(Build().GetProvider(name));
        }

        [Fact]
        public void An_unknown_provider_name_throws_NotFoundException()
        {
            var exception = Assert.Throws<NotFoundException>(() => Build().GetProvider("unknown-" + Suffix));

            Assert.Contains("unknown-" + Suffix, exception.Message);
        }
    }
}
