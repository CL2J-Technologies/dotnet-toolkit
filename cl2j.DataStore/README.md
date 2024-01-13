`cl2j.DataStore` is a multi-providers CRUD repoository abstraction library written in C#. It's an open and extensible framework based on interfaces and Dependency Injection.

Providers supported:

- `JSON` - Data store that use a JSON file for CRUD

The library also support a memory cache DataStore implemented as a [Decorator Pattern](https://en.wikipedia.org/wiki/Decorator_pattern). See below for more information.

# Getting started

The setup is simple.

1. Add the nuget package to your project:

```powershell
Install-Package cl2j.DataStore.Json
```

2. Add the following lines in the appsettings.json to configure the IFileStoreProvider:

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

> cl2j.DataStore use [cl2j.FileStorage](https://github.com/jfblier/cl2j.FileStorage) library to abstract the access to the file system. This allow to use different providers like Disk, Azure, AWS, etc.

3. Configures the services by calling `AddFileStorage()` and then `AddDataStoreListJson()` or `AddDataStoreDictionaryJson()`.

4. Get the `IDataStoreList` or `IDataStoreDictionary` configured and perform CRUD operatons.

`AddDataStoreListJson` receive three parameters:

1. The name of the FileStore to use. That's the name declared in the appsettings.json.
2. The name of the file were the data are persisted.
3. A function that return the unique key to identify the item.

## Web Application

```cs
public void ConfigureServices(IServiceCollection services)
{
  ...

  //Bootstrap the FileStorage to be available from DependencyInjection.
  //This will allow accessing IFileStorageProviderFactory instance
  services.AddFileStorage();

  //Configure the JSON DataStore.
  //The store will use the FileStorageProvider to access the file colors.json.
  //The field Id represent the key for the Color class
  services.AddDataStoreListJson<string, Color>("DataStore", "colors.json", (color) => color.Id);
}

public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
{
  ...

  //Add the Disk FileStorage provider
  app.ApplicationServices.UseFileStorageDisk();
}
```

## Console Application

```cs
var services = new ServiceCollection();
...

//Bootstrap the FileStorage to be available from DependencyInjection.
//This will allow accessing IFileStorageProviderFactory instance
services.AddFileStorage();

//Configure the JSON DataStore.
//The store will use the FileStorageProvider to access the file colors.json.
//The field Id represent the key for the Color class
services.AddDataStoreListJson<string, Color>("DataStore", "colors.json", (color) => color.Id);

//Add the Disk FileStorage provider
var serviceProvider = services.BuildServiceProvider();
serviceProvider.UseFileStorageDisk();
```

# Sample code

Here's a class that execute common operations.

```cs
internal class DataStoreSample
{
    private readonly IDataStoreList<string, Color> dataStoreColorRead;

    public DataStoreSample(IDataStoreList<string, Color> dataStoreColorRead)
    {
        this.dataStoreColorRead = dataStoreColorRead;
    }

    public async Task ExecuteAsync()
    {
        //Create colors
        await dataStoreColorRead.InsertAsync(new Color { Id = "red", Value = "#f00" });
        await dataStoreColorRead.InsertAsync(new Color { Id = "green", Value = "#0f0" });
        await dataStoreColorRead.InsertAsync(new Color { Id = "blue", Value = "#00f" });
        await dataStoreColorRead.InsertAsync(new Color { Id = "cyan", Value = "#0ff" });
        await dataStoreColorRead.InsertAsync(new Color { Id = "magenta", Value = "#f0f" });
        await dataStoreColorRead.InsertAsync(new Color { Id = "yellow", Value = "#ff0" });
        await dataStoreColorRead.InsertAsync(new Color { Id = "black", Value = "#000" });

        //Retreive all the colors
        var colors = await dataStoreColorRead.GetAllAsync();
        foreach (var color in colors)
            Debug.WriteLine($"{color.Id} -> {color.Value}");

        //Retreive the color black and update it's value
        var black = await dataStoreColorRead.GetByIdAsync("black");
        if (black != null)
        {
            black.Value = "#111";
            await dataStoreColorRead.UpdateAsync(black.Id, black);
        }

        //Delete a color
        await dataStoreColorRead.DeleteAsync("magenta");
    }
}
```

The class receive, by Dependency Injection, the `IDataStoreList`. The ExecuteAsync method insert colors, retreive all the colors inserted, modify the black color value and remove the magenta color.

A file named `colors.json` was created:

```json
[
  {
    "Id": "red",
    "Value": "#f00"
  },
  {
    "Id": "green",
    "Value": "#0f0"
  },
  {
    "Id": "blue",
    "Value": "#00f"
  },
  {
    "Id": "cyan",
    "Value": "#0ff"
  },
  {
    "Id": "yellow",
    "Value": "#ff0"
  },
  {
    "Id": "black",
    "Value": "#111"
  }
]
```

# Operations

Here's the operations available from the `IDataStoreList` interface:

```cs
public interface IDataStoreList<TKey, TValue>
{
    //Retreive all the items
    Task<IEnumerable<TValue>> GetAllAsync();

    //Get an item by it's key (Id)
    Task<TValue> GetByIdAsync(TKey key);

    //Insert a new item. The key must not exists
    Task InsertAsync(TValue entity);

    //Update an item
    Task UpdateAsync(TValue entity);

    //Delete an item
    Task DeleteAsync(TKey key);
}
```

Here's the operations available from the `IDataStoreDictionary` interface:

```cs
public interface IDataStoreDictionary<TKey, TValue>
{
    //Retreive all the items
    Task<Dictionary<TKey, TValue>> GetAllAsync();

    //Get an item by it's key (Id)
    Task<TValue> GetByIdAsync(TKey key);

    //Insert a new item. The key must not exists
    Task InsertAsync(TValue entity);

    //Update an item
    Task UpdateAsync(TValue entity);

    //Delete an item
    Task DeleteAsync(TKey key);
}
```

# Cache

Adding a memory cache, that is thread-safe, and refresh itself at specified interval is as simple as adding the following line in your startup or program:

```cs
services.AddDataStoreListJsonWithCache<string, Color>("Color", "DataStore", "colors.json", (color) => color.Id, TimeSpan.FromSeconds(5));
```

As the DataStoreCache is a `Decorator Pattern` and implement the `IDataStoreList` interface, using the cache is the same as using the `IDataStoreList`.

- `Cache name` Name of the cache - for logging purpose
- `FileStore name` Name of the FileStore to use. That's the name declared in the appsettings.json.
- `File name` Name of the file were the data are persisted.
- `Key identifier` A lambda expression that return the unique key of the item. In the example, It's the Id property of the Color class.
- `Refresh interval` A TimeSpan indicating the interval that the cache will be refreshed. In the example, we indicate that the cache will be refreshed every 5 seconds.

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
- `Cache` Add a simple builtin caching mecanism to have performant read DataStore
- Helper to convert to Dictionary DataStore
