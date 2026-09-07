using cl2j.DataStore.Dictionary;
using cl2j.DataStore.List;
using cl2j.Tooling.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace cl2j.DataStore.Tests
{
    /// <summary>
    ///     The factories are a name-to-store registry, and the interesting part is what they do
    ///     when the name is wrong: a store registered as one shape and asked for as another is the
    ///     mistake this design invites, since the name carries no type information.
    /// </summary>
    public class DataStoreDictionaryFactoryTests
    {
        private static DataStoreDictionaryFactory Factory() => new(new RecordingLogger<DataStoreDictionaryFactory>());

        [Fact]
        public async Task A_registered_store_comes_back_under_its_name()
        {
            var factory = Factory();
            var store = new InMemoryDictionaryStore<string, Person>();
            store.Seed("1", new Person("1", "Renée"));
            factory.AddDataStoreDictionaryCommandAndQuery("people", store);

            var resolved = factory.GetDataStoreDictionaryCommandAndQuery<string, Person>("people");

            Assert.Same(store, resolved);
            Assert.Equal("Renée", (await resolved.GetByIdAsync("1"))?.Name);
        }

        [Fact]
        public void The_same_store_answers_to_each_of_the_interfaces_it_implements()
        {
            //Registration is by name, not by interface: a command-and-query store is also a query
            //store and a load store, and a caller can ask for the narrowest one it needs.
            var factory = Factory();
            factory.AddDataStoreDictionaryCommandAndQuery("people", new InMemoryDictionaryStore<string, Person>());

            Assert.NotNull(factory.GetDataStoreDictionaryLoad<string, Person>("people"));
            Assert.NotNull(factory.GetDataStoreDictionaryQuery<string, Person>("people"));
            Assert.NotNull(factory.GetDataStoreDictionaryCommandAndQuery<string, Person>("people"));
        }

        [Fact]
        public void A_name_that_was_never_registered_is_reported_as_missing()
        {
            var exception = Assert.Throws<NotFoundException>(
                () => Factory().GetDataStoreDictionaryLoad<string, Person>("absent"));

            Assert.Contains("absent", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Asking_for_the_wrong_type_names_both_types()
        {
            //The failure this design has to be good at: the name matched but the store is of
            //another shape. The message has to say what was found as well as what was wanted, or
            //the caller has nothing to go on.
            var factory = Factory();
            factory.AddDataStoreDictionaryCommandAndQuery("people", new InMemoryDictionaryStore<string, Person>());

            var exception = Assert.Throws<ConflictException>(
                () => factory.GetDataStoreDictionaryCommandAndQuery<int, Person>("people"));

            Assert.Contains("InMemoryDictionaryStore", exception.Message, StringComparison.Ordinal);
            Assert.Contains("people", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Registering_a_name_twice_is_refused_rather_than_silently_replacing()
        {
            var factory = Factory();
            var first = new InMemoryDictionaryStore<string, Person>();
            factory.AddDataStoreDictionaryCommandAndQuery("people", first);

            Assert.Throws<ConflictException>(
                () => factory.AddDataStoreDictionaryCommandAndQuery("people", new InMemoryDictionaryStore<string, Person>()));

            Assert.Same(first, factory.GetDataStoreDictionaryCommandAndQuery<string, Person>("people"));
        }

        [Fact]
        public void Registering_a_store_says_so_in_the_log()
        {
            var logger = new RecordingLogger<DataStoreDictionaryFactory>();
            var factory = new DataStoreDictionaryFactory(logger);

            factory.AddDataStoreDictionaryCommandAndQuery("people", new InMemoryDictionaryStore<string, Person>());

            var entry = Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Information, entry.Level);
            Assert.Contains("people", entry.Message, StringComparison.Ordinal);
            //The pretty name, not the reflection spelling: "InMemoryDictionaryStore<String, Person>"
            //rather than "InMemoryDictionaryStore`2".
            Assert.Contains("InMemoryDictionaryStore<String, Person>", entry.Message, StringComparison.Ordinal);
        }
    }

    public class DataStoreListFactoryTests
    {
        private static DataStoreListFactory Factory() => new(new RecordingLogger<DataStoreListFactory>());

        private static InMemoryListStore<string, Person> Store() => new(p => p.Id);

        [Fact]
        public async Task A_registered_store_comes_back_under_its_name()
        {
            var factory = Factory();
            var store = Store();
            store.Seed(new Person("1", "Renée"));
            factory.AddDataStoreListCommandAndQuery("people", store);

            var resolved = factory.GetDataStoreListCommandAndQuery<string, Person>("people");

            Assert.Same(store, resolved);
            Assert.Equal("Renée", (await resolved.GetByIdAsync("1"))?.Name);
        }

        [Fact]
        public void A_load_only_store_can_be_registered_on_its_own()
        {
            var factory = Factory();
            factory.AddDataStoreListLoad<Person>("archive", new UnreadableListStore<Person>());

            Assert.NotNull(factory.GetDataStoreListLoad<Person>("archive"));
        }

        [Fact]
        public void A_load_only_store_cannot_be_asked_to_write()
        {
            //Worth pinning: nothing at registration time stops this, so the mistake surfaces at the
            //first resolve rather than at startup.
            var factory = Factory();
            factory.AddDataStoreListLoad<Person>("archive", new UnreadableListStore<Person>());

            Assert.Throws<ConflictException>(() => factory.GetDataStoreListCommandAndQuery<string, Person>("archive"));
        }

        [Fact]
        public void A_name_that_was_never_registered_is_reported_as_missing()
        {
            Assert.Throws<NotFoundException>(() => Factory().GetDataStoreListQuery<string, Person>("absent"));
        }

        [Fact]
        public void Registering_a_name_twice_is_refused()
        {
            var factory = Factory();
            factory.AddDataStoreListCommandAndQuery("people", Store());

            Assert.Throws<ConflictException>(() => factory.AddDataStoreListCommandAndQuery("people", Store()));
        }

        [Fact]
        public void The_two_factories_keep_separate_registries()
        {
            //They share a base class holding the dictionary, and that dictionary is an instance
            //field rather than a static one — so a name used by the list factory is still free for
            //the dictionary factory.
            var lists = Factory();
            var dictionaries = new DataStoreDictionaryFactory(new RecordingLogger<DataStoreDictionaryFactory>());

            lists.AddDataStoreListCommandAndQuery("people", Store());
            dictionaries.AddDataStoreDictionaryCommandAndQuery("people", new InMemoryDictionaryStore<string, Person>());

            Assert.NotNull(lists.GetDataStoreListCommandAndQuery<string, Person>("people"));
            Assert.NotNull(dictionaries.GetDataStoreDictionaryCommandAndQuery<string, Person>("people"));
        }
    }

    public class DataStoreRegistrationTests
    {
        [Fact]
        public void Registering_data_stores_resolves_both_factories_as_singletons()
        {
            //Singleton is the part that matters: a factory is a registry, and a registry that is
            //rebuilt per scope would lose everything registered at startup.
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDataStore();

            using var provider = services.BuildServiceProvider();

            var lists = provider.GetRequiredService<IDataStoreListFactory>();
            var dictionaries = provider.GetRequiredService<IDataStoreDictionaryFactory>();

            Assert.IsType<DataStoreListFactory>(lists);
            Assert.IsType<DataStoreDictionaryFactory>(dictionaries);
            Assert.Same(lists, provider.GetRequiredService<IDataStoreListFactory>());
            Assert.Same(dictionaries, provider.GetRequiredService<IDataStoreDictionaryFactory>());
        }

        [Fact]
        public void A_store_registered_at_startup_is_still_there_later()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDataStore();

            using var provider = services.BuildServiceProvider();
            provider.GetRequiredService<IDataStoreDictionaryFactory>()
                .AddDataStoreDictionaryCommandAndQuery("people", new InMemoryDictionaryStore<string, Person>());

            using var scope = provider.CreateScope();

            Assert.NotNull(scope.ServiceProvider.GetRequiredService<IDataStoreDictionaryFactory>()
                .GetDataStoreDictionaryCommandAndQuery<string, Person>("people"));
        }
    }
}
