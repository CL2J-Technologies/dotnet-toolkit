`cl2j.DataStore` is a multi-providers CRUD repository abstraction library written in C#. It's an open and extensible framework based on interfaces and Dependency Injection.

Two shapes of store are offered: a **list** (`IDataStoreList...`, items identified by a key read from the item, usually its `Id`) and a **dictionary** (`IDataStoreDictionary...`, items stored under an explicit key).

Packages:

- `cl2j.DataStore` - the interfaces, the named store registries (factories) and the memory cache decorators
- `cl2j.DataStore.Json` - Data store that use a JSON file for CRUD, through [cl2j.FileStorage](../cl2j.FileStorage/README.md)

The library also support a memory cache DataStore implemented as a [Decorator Pattern](https://en.wikipedia.org/wiki/Decorator_pattern). See below for more information.

# Getting started

The setup is simple.

1. Add the nuget package to your project (it brings `cl2j.DataStore` and `cl2j.FileStorage`):

```powershell
dotnet add package cl2j.DataStore.Json
```

2. Add the following lines in the appsettings.json to configure the IFileStorageProvider:

```json
"cl2j": {
    "FileStorage": {
        "Storages": {
            "DataStore": {
                "Type": "Disk",
                "Path": "<ThePathOfTheJSONFiles>"
            }
        }
    }
}
```

The settings configure a Disk FileStorage provider, which use `System.IO` classes.
Path is the local path, i.e. c:\dev, where the json file will be.

> cl2j.DataStore use the [cl2j.FileStorage](../cl2j.FileStorage/README.md) library to abstract the access to the file system. This allow to use different providers like Disk, Azure Blob Storage or S3.

3. Configures the services by calling `AddFileStorage()` and `AddDataStore()`, then, once the service provider is built, register your stores by name with the `CreateAndAdd...` extension methods, for example `CreateAndAddDataStoreListCommandAndQueryJsonWithCache` or `CreateAndAddDataStoreDictionaryJson`.

4. Get the store from the `IDataStoreListFactory` (or `IDataStoreDictionaryFactory`) and perform CRUD operations.

`CreateAndAddDataStoreListCommandAndQueryJsonWithCache` receive four parameters:

1. The name of the store. It is the name you will ask the factory for.
2. The name of the FileStorage to use. That's the name declared in the appsettings.json.
3. The name of the file were the data are persisted.
4. The interval at which the cache reloads the file.

The item type must implement `IEntity<TKey>` (an `Id` property of type `TKey`); `Entity<TKey>` is a ready-made base class. To use another property as the key, use the overload of `CreateDataStoreListCommandAndQueryJson...` that takes a `Func<TValue, TKey>`.

```cs
public class Color : Entity<string>
{
    public string Value { get; set; } = "";
}
```

## Web Application

```cs
var builder = WebApplication.CreateBuilder(args);

//The FileStorage factory resolves the IConfigurationRoot from the container
builder.Services.AddSingleton<IConfigurationRoot>(builder.Configuration);

//Bootstrap the FileStorage to be available from DependencyInjection.
builder.Services.AddFileStorage();

//Register the DataStore factories.
builder.Services.AddDataStore();

var app = builder.Build();

//Register the stores. The store use the FileStorageProvider "DataStore" to access the file colors.json.
//The Id property is the key of the Color class.
app.Services.CreateAndAddDataStoreListCommandAndQueryJsonWithCache<string, Color>("Colors", "DataStore", "colors.json", TimeSpan.FromSeconds(5));
```

## Console Application

```cs
var services = new ServiceCollection();
...

//The FileStorage factory resolves the IConfigurationRoot from the container
services.AddSingleton<IConfigurationRoot>(configuration);

services.AddLogging();
services.AddFileStorage();
services.AddDataStore();

var serviceProvider = services.BuildServiceProvider();

//Register the store. The store use the FileStorageProvider "DataStore" to access the file colors.json.
serviceProvider.CreateAndAddDataStoreListCommandAndQueryJsonWithCache<string, Color>("Colors", "DataStore", "colors.json", TimeSpan.FromSeconds(5));
```

The extension methods live in the `cl2j.DataStore.Json.List` and `cl2j.DataStore.Json.Dictionary` namespaces.

# Sample code

Here's a class that execute common operations.

```cs
using cl2j.DataStore.List;

internal class DataStoreSample
{
    private readonly IDataStoreListCommandAndQuery<string, Color> colors;

    public DataStoreSample(IDataStoreListFactory factory)
    {
        colors = factory.GetDataStoreListCommandAndQuery<string, Color>("Colors");
    }

    public async Task ExecuteAsync()
    {
        //Create colors
        await colors.InsertAsync(new Color { Id = "red", Value = "#f00" });
        await colors.InsertAsync(new Color { Id = "green", Value = "#0f0" });
        await colors.InsertAsync(new Color { Id = "blue", Value = "#00f" });
        await colors.InsertAsync(new Color { Id = "cyan", Value = "#0ff" });
        await colors.InsertAsync(new Color { Id = "magenta", Value = "#f0f" });
        await colors.InsertAsync(new Color { Id = "yellow", Value = "#ff0" });
        await colors.InsertAsync(new Color { Id = "black", Value = "#000" });

        //Retrieve all the colors
        var all = await colors.GetAllAsync();
        foreach (var color in all)
            Debug.WriteLine($"{color.Id} -> {color.Value}");

        //Retrieve the color black and update it's value
        var black = await colors.GetByIdAsync("black");
        if (black != null)
        {
            black.Value = "#111";
            await colors.UpdateAsync(black);
        }

        //Delete a color
        await colors.DeleteAsync("magenta");
    }
}
```

The class receive, by Dependency Injection, the `IDataStoreListFactory`. The ExecuteAsync method insert colors, retrieve all the colors inserted, modify the black color value and remove the magenta color.

A file named `colors.json` was created. It is written on one line, with camelCase property names (shown here on several lines for readability):

```json
[
  { "value": "#f00", "id": "red" },
  { "value": "#0f0", "id": "green" },
  { "value": "#00f", "id": "blue" },
  { "value": "#0ff", "id": "cyan" },
  { "value": "#ff0", "id": "yellow" },
  { "value": "#111", "id": "black" }
]
```

# Operations

Here's the operations available from the `IDataStoreListCommandAndQuery` interface (it extends `IDataStoreListQuery`, which extends `IDataStoreListLoad`):

```cs
public interface IDataStoreListCommandAndQuery<TKey, TValue>
{
    //Retrieve all the items
    Task<IReadOnlyList<TValue>> GetAllAsync();

    //Get an item by it's key (Id). null when it doesn't exist
    Task<TValue?> GetByIdAsync(TKey key);

    //Insert a new item. The key must not exists, otherwise a ConflictException is thrown
    Task InsertAsync(TValue entity);

    //Update an item. A NotFoundException is thrown when the key doesn't exist
    Task UpdateAsync(TValue entity);

    //Delete an item. A NotFoundException is thrown when the key doesn't exist
    Task DeleteAsync(TKey key);

    //Replace the whole content
    Task ReplaceAllByAsync(ICollection<TValue> items);
}
```

`ConflictException` and `NotFoundException` come from `cl2j.Tooling.Exceptions`.

Here's the operations available from the `IDataStoreDictionaryCommandAndQuery` interface (it extends `IDataStoreDictionaryQuery`, which extends `IDataStoreDictionaryLoad`):

```cs
public interface IDataStoreDictionaryCommandAndQuery<TKey, TValue> where TKey : notnull
{
    //Retrieve all the items
    Task<IReadOnlyDictionary<TKey, TValue>> GetAllAsync();

    //Get an item by it's key. null when it doesn't exist
    Task<TValue?> GetByIdAsync(TKey key);

    //Insert a new item. The key must not exists, otherwise an ArgumentException is thrown
    Task InsertAsync(TKey key, TValue entity);

    //Update an item. A KeyNotFoundException is thrown when the key doesn't exist
    Task UpdateAsync(TKey key, TValue entity);

    //Delete an item. Deleting a key that is not there is not an error
    Task DeleteAsync(TKey key);

    //Replace the whole content
    Task ReplaceAllByAsync(Dictionary<TKey, TValue> items);
}
```

The read-only interfaces `IDataStoreListLoad<TValue>` and `IDataStoreListQuery<TKey, TValue>` (and the dictionary equivalents) are available from the factories with `GetDataStoreListLoad`, `GetDataStoreListQuery`... Asking for a name that was not registered throws a `NotFoundException`; asking for a type the store doesn't implement throws a `ConflictException`. Registering the same name twice throws a `ConflictException`.

A store can also be created without being registered: `CreateDataStoreListCommandAndQueryJson<TKey, TValue>(fileStorageName, fileName)` and `CreateDataStoreDictionaryJson<TKey, TValue>(fileStorageName, fileName)` return the store itself.

## Entities with polymorphic members

The JSON stores read and write through [cl2j.FileStorage](../cl2j.FileStorage/README.md#json-and-type), which refuses every `$type` whose type is not on a list. By default the list is empty: an entity whose member holds a subclass (or a concrete type behind an interface), and a file that already holds a `$type`, make the store throw a `JsonSerializationException` instead of reading as the declared type and losing the derived properties.

> **Behaviour change (6.6.1).** Such an entity used to be written with a `$type` and read back. It now throws unless you list the types.

List them with the optional trailing `jsonOptions` argument, which every `...Json...` registration and creation method and every JSON store constructor takes:

```cs
var options = new JsonStorageOptions { AllowedTypes = [typeof(Dog), typeof(Cat)] };

serviceProvider.CreateAndAddDataStoreListCommandAndQueryJsonWithCache<string, Owner>("Owners", "DataStore", "owners.json", TimeSpan.FromSeconds(5), options);
```

`JsonStorageOptions` is in `cl2j.FileStorage.Extensions`. The archive (see below) takes the same optional trailing `jsonOptions`; without it, a snapshot of a polymorphic entity is refused and logged at Error, like any snapshot that cannot be written.

# Cache

Adding a memory cache, that is thread-safe, and refresh itself at specified interval is as simple as using the `...WithCache` variant of the registration in your startup or program:

```cs
serviceProvider.CreateAndAddDataStoreListCommandAndQueryJsonWithCache<string, Color>("Colors", "DataStore", "colors.json", TimeSpan.FromSeconds(5));
```

As the DataStoreCache is a `Decorator Pattern` and implement the `IDataStoreListCommandAndQuery` interface, using the cache is the same as using the store.

- `Cache name` Name of the cache, also the name the store is registered under. Used in the logs.
- `FileStore name` Name of the FileStore to use. That's the name declared in the appsettings.json.
- `File name` Name of the file were the data are persisted.
- `Refresh interval` A TimeSpan indicating the interval that the cache will be refreshed. In the example, we indicate that the cache will be refreshed every 5 seconds.

Reads are served from memory. Writes go to the underlying store first, then update the cache, so a failed write (for example a `ConflictException`) leaves the cache unchanged. A reload that fails is logged and the cache keeps serving what it has. If the very first load fails, reading throws an `InvalidOperationException` rather than answering as if the store were empty.

The same variants exist for a dictionary (`CreateAndAddDataStoreDictionaryJsonWithCache`) and for a read-only list (`CreateAndAddDataStoreListLoadJsonWithCache`).

## Warm-up

Every registered store that has a cache can be filled ahead of the first request. `GetWarmable()` on the factories returns them as `IDataStoreWarmable` (a `Name` and a `WarmAsync()` that completes once the cache has loaded, or throws an `InvalidOperationException` if the source can't be read). When to warm, and whether a failure stops the application, is up to the application:

```cs
var warmables = provider.GetRequiredService<IDataStoreListFactory>().GetWarmable()
    .Concat(provider.GetRequiredService<IDataStoreDictionaryFactory>().GetWarmable());

await Task.WhenAll(warmables.Select(w => w.WarmAsync())).WaitAsync(TimeSpan.FromSeconds(30));
```

## Archive

`DataStoreListCommandAndQueryArchive<TKey, TValue>` wraps a list store and, after every change, writes a snapshot of the whole store to a file storage. The file name is a format string in which `{0}` receives the timestamp (`yyyyMMdd-HHmm`), for example `colors-{0}.json`.

The archive is written after the store has accepted the change. If it cannot be written, the change is **not** reported as failed: the failure is logged at `Error` through the optional `logger` constructor argument (nothing is logged without one). Check that log if archives matter to you. Each snapshot reads the whole store back with `GetAllAsync()`.

# Feedback & Community

We look forward to hearing your comments.
Feel free to submit your opinion, any problems you have encountered as well as ideas for improvements, we would love to hear it.

If you have a technical question or issue, please either:

- Submit an issue
- Ask a question on StackOverflow
- Contact us directly

# Roadmap

- Add new implementation
  - `Database` implementation using reflection to create, modify and delete objects
- `CQRS` Use CQRS pattern to decouple the read and write. This will allow
- Helper to convert to Dictionary DataStore
