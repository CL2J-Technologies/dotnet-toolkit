using System.Text;
using cl2j.DataStore.Dictionary;
using cl2j.DataStore.List;
using cl2j.FileStorage.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace cl2j.DataStore.Tests
{
    /// <summary>
    ///     An entity with a key, used by every test here. Defined once so the tests share the type
    ///     arguments and not much else.
    /// </summary>
    public sealed class Person
    {
        public Person(string id, string name)
        {
            Id = id;
            Name = name;
        }

        public string Id { get; }
        public string Name { get; }
    }

    /// <summary>
    ///     The store the caches wrap. It is the real boundary — a database, a file — and a
    ///     dictionary in memory is enough to answer every question these tests ask: what reached
    ///     the underlying store, in what order, and what the cache did about it.
    /// </summary>
    public sealed class InMemoryDictionaryStore<TKey, TValue> : IDataStoreDictionaryCommandAndQuery<TKey, TValue> where TKey : notnull
    {
        private readonly Dictionary<TKey, TValue> items = [];

        public List<string> Calls { get; } = [];

        /// <summary>Awaited at the start of every call, so a test can hold one in flight.</summary>
        public Func<Task>? Gate { get; set; }

        /// <summary>
        ///     Whether inserting an existing key is accepted. The interface says the key must not
        ///     exist, and a store that enforces that is the normal case — but plenty of real ones
        ///     upsert, and that is where the cache's own behaviour becomes visible.
        /// </summary>
        public bool UpsertOnInsert { get; set; }

        private async Task PassGate()
        {
            if (Gate is not null)
                await Gate();
        }

        public async Task<Dictionary<TKey, TValue>> GetAllAsync()
        {
            Calls.Add("GetAll");
            await PassGate();
            //A copy: handing back the store's own dictionary would let a cache alias it and hide
            //whether anything was really written.
            return new Dictionary<TKey, TValue>(items);
        }

        public async Task<TValue?> GetByIdAsync(TKey key)
        {
            Calls.Add($"GetById({key})");
            await PassGate();
            return items.TryGetValue(key, out var value) ? value : default;
        }

        public async Task InsertAsync(TKey key, TValue entity)
        {
            Calls.Add($"Insert({key})");
            await PassGate();
            if (UpsertOnInsert)
                items[key] = entity;
            else
                items.Add(key, entity);
        }

        public Task UpdateAsync(TKey key, TValue entity)
        {
            Calls.Add($"Update({key})");
            items[key] = entity;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(TKey key)
        {
            Calls.Add($"Delete({key})");
            items.Remove(key);
            return Task.CompletedTask;
        }

        public Task ReplaceAllByAsync(Dictionary<TKey, TValue> items)
        {
            Calls.Add("ReplaceAll");
            this.items.Clear();
            foreach (var (key, value) in items)
                this.items[key] = value;
            return Task.CompletedTask;
        }

        public IReadOnlyDictionary<TKey, TValue> Contents => items;

        public void Seed(TKey key, TValue value) => items[key] = value;
    }

    public sealed class InMemoryListStore<TKey, TValue> : IDataStoreListCommandAndQuery<TKey, TValue>
    {
        private readonly List<TValue> items = [];
        private readonly Func<TValue, TKey> getKey;

        public InMemoryListStore(Func<TValue, TKey> getKey) => this.getKey = getKey;

        public List<string> Calls { get; } = [];

        public Task<List<TValue>> GetAllAsync()
        {
            Calls.Add("GetAll");
            return Task.FromResult(new List<TValue>(items));
        }

        public Task<TValue?> GetByIdAsync(TKey key)
        {
            Calls.Add("GetById");
            return Task.FromResult(items.FirstOrDefault(i => EqualityComparer<TKey>.Default.Equals(getKey(i), key)));
        }

        public Task InsertAsync(TValue entity)
        {
            Calls.Add("Insert");
            items.Add(entity);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(TValue entity)
        {
            Calls.Add("Update");
            var index = items.FindIndex(i => EqualityComparer<TKey>.Default.Equals(getKey(i), getKey(entity)));
            if (index >= 0)
                items[index] = entity;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(TKey key)
        {
            Calls.Add("Delete");
            items.RemoveAll(i => EqualityComparer<TKey>.Default.Equals(getKey(i), key));
            return Task.CompletedTask;
        }

        public Task ReplaceAllByAsync(ICollection<TValue> items)
        {
            Calls.Add("ReplaceAll");
            this.items.Clear();
            this.items.AddRange(items);
            return Task.CompletedTask;
        }

        public IReadOnlyList<TValue> Contents => items;

        public void Seed(params TValue[] values) => items.AddRange(values);
    }

    /// <summary>A store whose reads fail, to show what a cache does when the source is down.</summary>
    public sealed class UnreadableListStore<TValue> : IDataStoreListLoad<TValue>
    {
        public int Attempts { get; private set; }

        public Task<List<TValue>> GetAllAsync()
        {
            Attempts++;
            throw new InvalidOperationException("the source is unavailable");
        }
    }

    public sealed class UnreadableDictionaryStore<TKey, TValue> : IDataStoreDictionaryLoad<TKey, TValue> where TKey : notnull
    {
        public Task<Dictionary<TKey, TValue>> GetAllAsync() => throw new InvalidOperationException("the source is unavailable");
    }

    public sealed class RecordingObserver<T> : Tooling.Observers.IObserver<T>
    {
        public List<T> Notifications { get; } = [];

        public Task OnChangeAsync(T t)
        {
            Notifications.Add(t);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    ///     A file storage that keeps what it was given in memory, so a test can ask which name was
    ///     written rather than go looking on a disk for it.
    /// </summary>
    public sealed class InMemoryFileStorage : IFileStorageProvider
    {
        public Dictionary<string, string> Files { get; } = [];

        public List<string> WritesInOrder { get; } = [];

        public string Name { get; set; } = "memory";

        public void Initialize(string providerName, IConfigurationSection configuration)
        {
        }

        public Task<bool> ExistsAsync(string name) => Task.FromResult(Files.ContainsKey(name));

        public Task<FileStoreFileInfo?> GetInfoAsync(string name) => Task.FromResult<FileStoreFileInfo?>(null);

        public Task<IEnumerable<string>> ListFilesAsync(string path) => Task.FromResult<IEnumerable<string>>(Files.Keys);

        public Task<IEnumerable<string>> ListFoldersAsync(string path) => Task.FromResult<IEnumerable<string>>([]);

        public Task<bool> ReadAsync(string name, Stream stream)
        {
            if (!Files.TryGetValue(name, out var content))
                return Task.FromResult(false);

            var bytes = Encoding.UTF8.GetBytes(content);
            stream.Write(bytes, 0, bytes.Length);
            stream.Position = 0;
            return Task.FromResult(true);
        }

        /// <summary>Set to show what happens when the archive cannot be written.</summary>
        public bool FailWrites { get; set; }

        public async Task WriteAsync(string name, Stream stream, string? contentType = null)
        {
            if (FailWrites)
                throw new IOException("the archive destination is unavailable");

            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            Files[name] = await reader.ReadToEndAsync();
            WritesInOrder.Add(name);
        }

        public async Task AppendAsync(string name, Stream stream)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            var appended = await reader.ReadToEndAsync();
            Files[name] = Files.TryGetValue(name, out var existing) ? existing + appended : appended;
        }

        public Task DeleteAsync(string name)
        {
            Files.Remove(name);
            return Task.CompletedTask;
        }
    }

    /// <summary>Keeps what was logged, so a test can assert that a failure was reported.</summary>
    public sealed class RecordingLogger : ILogger
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (Entries)
                Entries.Add((logLevel, formatter(state, exception), exception));
        }
    }

    public sealed class RecordingLogger<T> : ILogger<T>
    {
        private readonly RecordingLogger inner = new();

        public List<(LogLevel Level, string Message, Exception? Exception)> Entries => inner.Entries;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => inner.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel) => inner.IsEnabled(logLevel);

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => inner.Log(logLevel, eventId, state, exception, formatter);
    }
}
