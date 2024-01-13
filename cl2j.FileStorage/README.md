`cl2j.FileStorage` is a multi-providers .NET library written in C# that abstract file operations like read, write, delete and more. It's an open and extensible framework based on interfaces and Dependency Injection.

Providers supported:

- `FileStorageProviderDisk` provider for Local file system
- `FileStorageProviderAzureBlobStorage` provider for Azure Blob Storage

# Getting started

The setup is simple.

Add the nuget package to your project:

```powershell
Install-Package cl2j.FileStorage
```

1. Add the following lines in the appsettings.json:

```json
"cl2j": {
  "FileStorage": {
    "Storages": {
      "Data": {
        "Type": "Disk",
        "Path": "c:\folder>"
      }
    }
  }
}
```

In the previous example, Data is the name of the FileStorage.
Type is the type of the provider, Disk indicate that you want to use a provider that will use the File.IO operations.
Path is the local path, i.e. c:\dev, where the file operations will be done.

2. Configures the services by calling **AddFileStorage()** and then **UseFileStorageDisk()**
3. Get the IFileProvider configured and perform file operations

IFileStorageProvider works with Streams. The library offer utilities, through extensions methods, to simplify text manipulations.

## Web Application

```cs
public void ConfigureServices(IServiceCollection services)
{
  ...

  //Bootstrap the FileStorage to be available from DependencyInjection.
  //This will allow accessing IFileStorageProviderFactory instance
  services.AddFileStorage();
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

//Add the Disk FileStorage provider
var serviceProvider = services.BuildServiceProvider();
serviceProvider.UseFileStorageDisk();
```

# Sample code

Here's a class that execute common file operations.

The class receive, by Dependency Injection, the IFileStorageProviderFactory. It request the IFileStorageProvider, represented by the name Data, and execute file operations.

```cs
internal class FileOperationSample
{
    private IFileStorageProvider fileStorageProvider;

    public FileOperationExecutor(IFileStorageProviderFactory fileStorageFactory)
    {
      //Retreive the FileStorage provider
      fileStorageProvider = fileStorageFactory.Get("Data");
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

To perform file operations, you need to get the IFileStorageProviderFactory and request the provider by passing it's name like:

```cs
var fileStorageProviderFactory = serviceProvider.GetRequiredService<IFileStorageProviderFactory>();
var fileStorageProvider = fileStorageProviderFactory.Get("Data");
```

Here's the operations available from the `IFileStorageProvider` interface:

```cs
public interface IFileStorageProvider
{
  // Write content
  Task WriteAsync(string name, Stream stream);

  // Read content
  Task<bool> ReadAsync(string name, Stream stream);

  // Append content
  Task AppendAsync(string name, Stream stream);

  // Validate if a file exists
  Task<bool> ExistsAsync(string name);

  // List files of directory
  Task<IEnumerable<string>> ListAsync(string path);

  // Get file information
  Task<FileStoreFileInfo> GetInfoAsync(string name);

  // Delete file
  Task DeleteAsync(string name);
}
```

# Azure Blob Storage

To use a Azure Blob Storage in youf project, add the following package:

```powershell
Install-Package cl2j.FileStorage.Provider.AzureBlobStorage
```

In the application configuration, call `UseFileStorageAzureBlobStorage()` like this:

```cs
serviceProvider.UseFileStorageAzureBlobStorage();
```

Add an entry in the appsetttings.json as follow:

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

# Feedback & Community

We look forward to hearing your comments.
Feel free to submit your opinion, any problems you have encountered as well as ideas for improvements, we would love to hear it.

If you have a technical question or issue, please either:

- Submit an issue
- Ask a question on StackOverflow
- Contact us directly

# Roadmap

We expect to add `Amazon S3` and `Google Cloud Storage` in the coming months.

We will also like to add:

- `FTP` FileStorageProvider
- Copy and move utilities to simplify the operations
