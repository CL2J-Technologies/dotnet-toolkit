using cl2j.DataStore.Json.Dictionary;
using cl2j.DataStore.Json.List;
using cl2j.FileStorage.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Xunit;

namespace cl2j.DataStore.Json.Tests
{
    /// <summary>
    ///     What the JSON stores do with an entity whose runtime type is a subclass of the declared
    ///     one. By default a <c>$type</c> is refused, on read and on write, because following it
    ///     would let the file choose a type to instantiate and dropping it would lose the derived
    ///     properties without a word. A store is told which subclasses to expect with
    ///     <see cref="JsonStorageOptions"/>.
    /// </summary>
    public sealed class DataStoreJsonPolymorphismTests : IDisposable
    {
        private const string FileName = "animals.json";

        private readonly JsonStoreEnvironment environment = new();

        public void Dispose() => environment.Dispose();

        public class Animal
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
        }

        public sealed class Dog : Animal
        {
            public string Breed { get; set; } = "";
        }

        private static readonly JsonStorageOptions AllowDogs = new() { AllowedTypes = [typeof(Dog)] };

        private static readonly string DogType = $"{typeof(Dog).FullName}, {typeof(Dog).Assembly.GetName().Name}";

        private DataStoreListCommandAndQueryJson<string, Animal> ListStore(JsonStorageOptions? options = null)
            => new(environment.Provider, FileName, a => a.Id, NullLogger.Instance, jsonOptions: options);

        private DataStoreDictionaryCommandAndQueryJson<string, Animal> DictionaryStore(JsonStorageOptions? options = null)
            => new(environment.Provider, FileName, NullLogger.Instance, jsonOptions: options);

        [Fact]
        public async Task A_list_store_refuses_to_write_a_subclass_by_default()
        {
            var store = ListStore();

            await Assert.ThrowsAsync<JsonSerializationException>(() => store.InsertAsync(new Dog { Id = "1", Breed = "lab" }));

            Assert.False(await environment.Provider.ExistsAsync(FileName));
        }

        [Fact]
        public async Task A_list_store_refuses_to_read_a_file_with_a_type_by_default()
        {
            await environment.Provider.WriteTextAsync(FileName, $"[{{\"$type\":\"{DogType}\",\"id\":\"1\",\"breed\":\"lab\"}}]");

            await Assert.ThrowsAsync<JsonSerializationException>(() => ListStore().GetAllAsync());
        }

        [Fact]
        public async Task A_list_store_without_subclasses_still_round_trips()
        {
            var store = ListStore();

            await store.InsertAsync(new Animal { Id = "1", Name = "Rex" });

            Assert.Equal("Rex", (await store.GetByIdAsync("1"))?.Name);
        }

        [Fact]
        public async Task A_list_store_with_the_subclass_allowed_round_trips_it()
        {
            var store = ListStore(AllowDogs);

            await store.InsertAsync(new Dog { Id = "1", Name = "Rex", Breed = "lab" });
            await store.InsertAsync(new Animal { Id = "2", Name = "Tom" });

            var all = await store.GetAllAsync();
            Assert.Equal("lab", Assert.IsType<Dog>(all[0]).Breed);
            Assert.IsNotType<Dog>(all[1]);
        }

        [Fact]
        public async Task A_list_store_with_the_subclass_allowed_still_refuses_any_other_type()
        {
            await environment.Provider.WriteTextAsync(FileName, "[{\"$type\":\"System.Diagnostics.Process, System.Diagnostics.Process\",\"id\":\"1\"}]");

            await Assert.ThrowsAsync<JsonSerializationException>(() => ListStore(AllowDogs).GetAllAsync());
        }

        [Fact]
        public async Task A_dictionary_store_refuses_a_subclass_by_default_and_round_trips_it_when_allowed()
        {
            await Assert.ThrowsAsync<JsonSerializationException>(() => DictionaryStore().InsertAsync("1", new Dog { Id = "1", Breed = "lab" }));

            var store = DictionaryStore(AllowDogs);
            await store.InsertAsync("1", new Dog { Id = "1", Breed = "lab" });

            Assert.Equal("lab", Assert.IsType<Dog>(await store.GetByIdAsync("1")).Breed);
        }

        [Fact]
        public async Task A_dictionary_store_refuses_to_read_a_file_with_a_type_by_default()
        {
            await environment.Provider.WriteTextAsync(FileName, $"{{\"1\":{{\"$type\":\"{DogType}\",\"breed\":\"lab\"}}}}");

            await Assert.ThrowsAsync<JsonSerializationException>(() => DictionaryStore().GetAllAsync());
            Assert.Equal("lab", Assert.IsType<Dog>(await DictionaryStore(AllowDogs).GetByIdAsync("1")).Breed);
        }

        [Fact]
        public async Task A_read_only_list_store_takes_the_same_option()
        {
            await environment.Provider.WriteTextAsync(FileName, $"[{{\"$type\":\"{DogType}\",\"id\":\"1\",\"breed\":\"lab\"}}]");

            await Assert.ThrowsAsync<JsonSerializationException>(() => new DataStoreListLoadJson<Animal>(environment.Provider, FileName, NullLogger.Instance).GetAllAsync());

            var all = await new DataStoreListLoadJson<Animal>(environment.Provider, FileName, NullLogger.Instance, AllowDogs).GetAllAsync();
            Assert.Equal("lab", Assert.IsType<Dog>(Assert.Single(all)).Breed);
        }

        [Fact]
        public async Task A_read_only_dictionary_store_takes_the_same_option()
        {
            await environment.Provider.WriteTextAsync(FileName, $"{{\"1\":{{\"$type\":\"{DogType}\",\"breed\":\"lab\"}}}}");

            await Assert.ThrowsAsync<JsonSerializationException>(() => new DataStoreDictionaryLoadJson<string, Animal>(environment.Provider, FileName, NullLogger.Instance).GetAllAsync());

            var all = await new DataStoreDictionaryLoadJson<string, Animal>(environment.Provider, FileName, NullLogger.Instance, AllowDogs).GetAllAsync();
            Assert.Equal("lab", Assert.IsType<Dog>(all["1"]).Breed);
        }

        [Fact]
        public async Task The_registration_extensions_pass_the_option_to_the_store()
        {
            var list = environment.Services.CreateDataStoreListCommandAndQueryJson<string, DogEntity>(JsonStoreEnvironment.StorageName, FileName, jsonOptions: AllowDogs);
            await list.InsertAsync(new DogEntity { Id = "1" });
            Assert.Equal("lab", Assert.IsType<Dog>((await list.GetByIdAsync("1"))!.Pet).Breed);

            var dictionary = environment.Services.CreateDataStoreDictionaryJson<string, Animal>(JsonStoreEnvironment.StorageName, "dictionary.json", AllowDogs);
            await dictionary.InsertAsync("1", new Dog { Breed = "lab" });
            Assert.Equal("lab", Assert.IsType<Dog>(await dictionary.GetByIdAsync("1")).Breed);
        }

        public class DogEntity : Entity<string>
        {
            public Animal? Pet { get; set; } = new Dog { Breed = "lab" };
        }
    }
}
