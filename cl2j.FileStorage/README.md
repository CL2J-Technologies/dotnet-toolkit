`cl2j.FileStorage` is a multi-providers .NET library written in C# that abstracts file operations like read, write, delete and more. It's an open and extensible framework based on interfaces and Dependency Injection.

Providers supported:

- `FileStorageProviderDisk` provider for Local file system (built in `cl2j.FileStorage`)
- `FileStorageProviderAzureBlobStorage` provider for Azure Blob Storage (`cl2j.FileStorage.Provider.AzureBlobStorage`)
- `FileStorageProviderS3` provider for S3-compatible object storage (Amazon S3, Cloudflare R2, MinIO) (`cl2j.FileStorage.Provider.S3`)

# Getting started

The setup is simple.

Add the nuget package to your project:

```powershell
dotnet add package cl2j.FileStorage
```

1. Add the following lines in the appsettings.json:

```json
"cl2j": {
  "FileStorage": {
    "Storages": {
      "Data": {
        "Type": "Disk",
        "Path": "c:\\folder"
      }
    }
  }
}
```

In the previous example, Data is the name of the FileStorage.
Type is the type of the provider, Disk indicate that you want to use a provider that will use the File.IO operations.
Path is the local path, i.e. c:\dev, where the file operations will be done. The folder is created if it does not exist.

2. Register the `IConfigurationRoot` and call **AddFileStorage()**. The disk provider is built in; the other providers add their own registration (see below).
3. Get the `IFileStorageProvider` configured, by name, from the `IFileStorageFactory`, and perform file operations

IFileStorageProvider works with Streams. The library offer utilities, through extensions methods, to simplify text, JSON and binary manipulations.

The factory reads the `cl2j:FileStorage:Storages` section from an `IConfigurationRoot` that it resolves from the container, so that
instance must be registered.

## Web Application

```cs
var builder = WebApplication.CreateBuilder(args);

//The FileStorage factory resolves the IConfigurationRoot from the container
builder.Services.AddSingleton<IConfigurationRoot>(builder.Configuration);

//Bootstrap the FileStorage to be available from DependencyInjection.
//This will allow accessing the IFileStorageFactory instance
builder.Services.AddFileStorage();
```

## Console Application

```cs
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var services = new ServiceCollection();
services.AddSingleton<IConfigurationRoot>(configuration);

//Bootstrap the FileStorage to be available from DependencyInjection.
//This will allow accessing the IFileStorageFactory instance
services.AddFileStorage();

var serviceProvider = services.BuildServiceProvider();
```

# Sample code

Here's a class that execute common file operations.

The class receive, by Dependency Injection, the IFileStorageFactory. It request the IFileStorageProvider, represented by the name Data, and execute file operations.

```cs
using cl2j.FileStorage.Core;
using cl2j.FileStorage.Extensions;

internal class FileOperationSample
{
    private readonly IFileStorageProvider fileStorageProvider;

    public FileOperationSample(IFileStorageFactory fileStorageFactory)
    {
      //Retrieve the FileStorage provider
      fileStorageProvider = fileStorageFactory.GetProvider("Data");
    }

    public async Task ExecuteAsync()
    {
      //Create a file with the specified text
      await fileStorageProvider.WriteTextAsync("file1.txt", "First part of the text");

      //Read the file content
      var text = await fileStorageProvider.ReadTextAsync("file1.txt");

      //Append text to the existing file
      await fileStorageProvider.AppendTextAsync("file1.txt", "Second part of the text");

      //Delete the file
      await fileStorageProvider.DeleteAsync("file1.txt");
    }
}
```

# Operations

## Get the File Storage Provider

To perform file operations, you need to get the IFileStorageFactory and request the provider by passing it's name like:

```cs
var fileStorageFactory = serviceProvider.GetRequiredService<IFileStorageFactory>();
var fileStorageProvider = fileStorageFactory.GetProvider("Data");
```

`GetProvider` throws a `NotFoundException` when no storage of that name is configured. `serviceProvider.GetFileStorageProvider("Data")` does both steps in one call.

Here's the operations available from the `IFileStorageProvider` interface:

```cs
public interface IFileStorageProvider
{
  // Name of the storage, as declared in the configuration
  string Name { get; set; }

  // Initialize itself from its configuration section (called by the factory)
  void Initialize(string providerName, IConfigurationSection configuration);

  // Validate if a file exists
  Task<bool> ExistsAsync(string name);

  // Get file information. null when the file doesn't exist
  Task<FileStoreFileInfo?> GetInfoAsync(string name);

  // List the files directly in a path
  Task<IEnumerable<string>> ListFilesAsync(string path);

  // List the files directly in a path whose name starts with a prefix
  Task<IEnumerable<string>> ListFilesAsync(string path, string namePrefix);

  // List the folders directly in a path
  Task<IEnumerable<string>> ListFoldersAsync(string path);

  // Read content into a stream. false when the file doesn't exist
  Task<bool> ReadAsync(string name, Stream stream);

  // Write content. An existing file is overwritten
  Task WriteAsync(string name, Stream stream, string? contentType = null);

  // Append content. The file is created when it doesn't exist
  Task AppendAsync(string name, Stream stream);

  // Delete file
  Task DeleteAsync(string name);
}
```

## Extension methods

The `cl2j.FileStorage.Extensions` namespace adds helpers on `IFileStorageProvider`:

- `ReadTextAsync`, `WriteTextAsync`, `AppendTextAsync` for text (UTF-8 unless an `Encoding` is given). `ReadTextAsync` returns an empty string when the file doesn't exist.
- `ReadJsonObjectAsync<T>`, `WriteJsonObjectAsync` for JSON objects. A file name ending in `.json.gz` is compressed on write and decompressed on read.
  Type names (`$type`) are **off by default**: see [JSON and `$type`](#json-and-type).
- `ReadBytesAsync`, `ReadObjectAsync`, `WriteBytesAsync` for binary content.
- `ClearDirectoryAsync(path)` deletes the files of a path and returns how many were found.
- `Copy(source, destination)` copies a file inside the same storage and returns `true` on success.
- `BufferedFileStorage` buffers appended text in memory and writes it at an interval, with a file name pattern and size-based rollover. It is what [cl2j.Logging](../cl2j.Logging/README.md) uses to write log files.

## JSON and `$type`

A `$type` property in a JSON file names the .NET type to instantiate. If anyone who can write to the storage (a shared folder, a bucket, an uploaded file) could choose that type, they would choose what the reader constructs. So `ReadJsonObjectAsync`, `WriteJsonObjectAsync` and `SerializeJsonObject` neither write nor follow a `$type` unless you list the types you expect.

> **Behaviour change (6.6.1).** These helpers used to read with `TypeNameHandling.Auto` and no binder. A `$type` is now ignored by default: a polymorphic member comes back as its **declared type**, and the properties only the derived type has are dropped. If you read a file and write it back, they are gone from the file. Files that were written with `$type` need the opt-in below.

To keep polymorphism, pass the types you expect:

```cs
var options = new JsonStorageOptions { AllowedTypes = [typeof(Circle), typeof(Square)] };

await provider.WriteJsonObjectAsync("shapes.json", holder, options);
var read = await provider.ReadJsonObjectAsync<Holder>("shapes.json", options);
```

- With a list, `$type` is written for a polymorphic value and resolved on read, but **only** to a type on the list. Any other `$type` throws a `JsonSerializationException`, and nothing is instantiated. The check holds for members that ask for type names through `[JsonProperty(TypeNameHandling = ...)]` as well.
- Writing a value whose type is not on the list also throws, because the file could not be read back.
- A closed generic type is matched with its arguments: list `typeof(List<Shape>)` for a member declared as `object` that holds one. The version and token of an assembly in a stored name are ignored.
- `cl2j.DataStore.Json` does not offer the option: its stores read and write without type names.

# Azure Blob Storage

To use a Azure Blob Storage in your project, add the following package:

```powershell
dotnet add package cl2j.FileStorage.Provider.AzureBlobStorage
```

In the application configuration, register the provider (it also calls `AddFileStorage()` for you):

```cs
using cl2j.FileStorage.Provider.AzureBlobStorage;

services.AddAzureBlobFileStorage();
```

Add an entry in the appsettings.json as follow:

```json
"cl2j": {
  "FileStorage": {
    "Storages": {
      "Azure": {
        "Type": "AzureBlobStorage",
        "ConnectionString": "<ConnectionStringToYourStorage>",
        "Container": "<NameOfYourContainer>"
      }
    }
  }
}
```

| Setting | Default | |
| --- | --- | --- |
| `ConnectionString` | — | The connection string of the storage account. |
| `Container` | — | Required. |
| `CreateIfMissing` | `false` | Creates the container when it is not there. Off by default: a missing container is nearly always a name spelled wrong, so the provider fails to initialize with a `NotFoundException` rather than hide the mistake. |

# S3-compatible object storage

Works with Amazon S3 and with anything that speaks the same API — Cloudflare R2, MinIO, Backblaze B2.

```powershell
dotnet add package cl2j.FileStorage.Provider.S3
```

```cs
using cl2j.FileStorage.Provider.S3;

services.AddS3FileStorage();
```

For Amazon S3, name the region:

```json
"cl2j": {
  "FileStorage": {
    "Storages": {
      "Medias": {
        "Type": "S3",
        "Bucket": "<YourBucket>",
        "AccessKey": "<YourAccessKey>",
        "SecretKey": "<YourSecretKey>",
        "Region": "us-east-1"
      }
    }
  }
}
```

For every other implementation, give the endpoint instead of the region:

```json
"ServiceUrl": "https://<AccountId>.r2.cloudflarestorage.com"
```

| Setting | Default | |
| --- | --- | --- |
| `Bucket` | — | Required. |
| `AccessKey`, `SecretKey` | — | Both required. |
| `ServiceUrl` | — | The endpoint, for an implementation that is not Amazon's. |
| `Region` | — | The AWS region. Ignored when `ServiceUrl` is set. |
| `ForcePathStyle` | `true` | Addresses objects as `endpoint/bucket/key`. Required by MinIO and most self-hosted implementations; accepted by R2. |
| `CreateIfMissing` | `false` | Creates the bucket when it is not there. Off by default: a missing bucket is nearly always a name spelled wrong, and creating it hides the mistake behind data written where no one will look. |

## Two things to know before choosing it

**`AppendAsync` reads, concatenates, and writes the whole object back**, because S3 has no append.
The cost of appending therefore grows with the size of what is already there, and two overlapping
appends leave only one of them with no error anywhere. Azure Blob has a real append block and does
not have either problem — worth knowing when deciding where a log goes.

**Listings are grouped on `/`**, so `ListFilesAsync` returns what sits directly under a prefix
rather than everything beneath it, the same way the other providers behave. Object storage itself
is flat: a key containing slashes only looks like a path.

**A listing can be narrowed by name**: `ListFilesAsync(path, namePrefix)` returns only the files in
the path whose name starts with the prefix, and the service does the narrowing, so a bucket too
large to list whole can be walked one slice at a time. Azure Blob narrows the same way; the disk
provider and any other implementation fall back on listing the path and filtering.

# Feedback & Community

We look forward to hearing your comments.
Feel free to submit your opinion, any problems you have encountered as well as ideas for improvements, we would love to hear it.

If you have a technical question or issue, please either:

- Submit an issue
- Ask a question on StackOverflow
- Contact us directly

# Roadmap

We expect to add `Google Cloud Storage` in the coming months.

We will also like to add:

- `FTP` FileStorageProvider
- A move utility, to complement `Copy`
