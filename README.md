# dotnet-toolkit

A collection of small, independent .NET libraries from CL2J Technologies: data access, file storage,
caching, logging, image handling, object mapping, runtime C# compilation and ASP.NET Core helpers.
Each library is published to NuGet.org as its own package, so you reference only what you use.

All packages target **net10.0** and share a single version number: every release publishes the whole
set together (see [Versioning](CONTRIBUTING.md#versioning)).

## Packages

| Package | NuGet | Description |
|---|---|---|
| [cl2j.Tooling](cl2j.Tooling/README.md) | [![NuGet](https://img.shields.io/nuget/v/cl2j.Tooling)](https://www.nuget.org/packages/cl2j.Tooling) | General-purpose .NET utilities: caching, compression, configuration helpers, and DI extensions. |
| [cl2j.Smapper](cl2j.Smapper/README.md) | [![NuGet](https://img.shields.io/nuget/v/cl2j.Smapper)](https://www.nuget.org/packages/cl2j.Smapper) | Lightweight reflection-based object-to-object mapper. |
| [cl2j.Scripting](cl2j.Scripting/README.md) | [![NuGet](https://img.shields.io/nuget/v/cl2j.Scripting)](https://www.nuget.org/packages/cl2j.Scripting) | Dynamic C# code compilation and execution at runtime. |
| [cl2j.Image](cl2j.Image/README.md) | [![NuGet](https://img.shields.io/nuget/v/cl2j.Image)](https://www.nuget.org/packages/cl2j.Image) | Image utilities: reading, EXIF orientation, resizing, thumbnails, JPEG and WebP encoding and duplicate detection. Cross-platform, on SkiaSharp. |
| [cl2j.WebTooling](cl2j.WebTooling/README.md) | [![NuGet](https://img.shields.io/nuget/v/cl2j.WebTooling)](https://www.nuget.org/packages/cl2j.WebTooling) | ASP.NET Core utilities: Claims, HttpClient, and HttpRequest extensions. |
| [cl2j.FileStorage](cl2j.FileStorage/README.md) | [![NuGet](https://img.shields.io/nuget/v/cl2j.FileStorage)](https://www.nuget.org/packages/cl2j.FileStorage) | Multi-provider file storage abstraction: read, write, delete, and more. Extensible via Dependency Injection. |
| [cl2j.FileStorage.Provider.AzureBlobStorage](cl2j.FileStorage/README.md) | [![NuGet](https://img.shields.io/nuget/v/cl2j.FileStorage.Provider.AzureBlobStorage)](https://www.nuget.org/packages/cl2j.FileStorage.Provider.AzureBlobStorage) | Azure Blob Storage provider for cl2j.FileStorage. |
| [cl2j.FileStorage.Provider.S3](cl2j.FileStorage/README.md) | [![NuGet](https://img.shields.io/nuget/v/cl2j.FileStorage.Provider.S3)](https://www.nuget.org/packages/cl2j.FileStorage.Provider.S3) | S3-compatible object storage provider for cl2j.FileStorage. Works with Amazon S3, Cloudflare R2, MinIO and other implementations of the S3 API. |
| [cl2j.Logging](cl2j.Logging/README.md) | [![NuGet](https://img.shields.io/nuget/v/cl2j.Logging)](https://www.nuget.org/packages/cl2j.Logging) | Logging extensions and unhandled exception handling for ASP.NET Core applications. |
| [cl2j.DataStore](cl2j.DataStore/README.md) | [![NuGet](https://img.shields.io/nuget/v/cl2j.DataStore)](https://www.nuget.org/packages/cl2j.DataStore) | Multi-provider CRUD repository abstraction. Extensible via Dependency Injection. |
| [cl2j.DataStore.Json](cl2j.DataStore/README.md) | [![NuGet](https://img.shields.io/nuget/v/cl2j.DataStore.Json)](https://www.nuget.org/packages/cl2j.DataStore.Json) | JSON file provider for cl2j.DataStore. |
| [cl2j.Database](cl2j.Database/README.md) | [![NuGet](https://img.shields.io/nuget/v/cl2j.Database)](https://www.nuget.org/packages/cl2j.Database) | Database abstraction layer with connection and query extensions. |
| [cl2j.Database.SqlServer](cl2j.Database/README.md) | [![NuGet](https://img.shields.io/nuget/v/cl2j.Database.SqlServer)](https://www.nuget.org/packages/cl2j.Database.SqlServer) | SQL Server provider for cl2j.Database. |
| cl2j.DataStore.Database-deprecated (**deprecated**) | [![NuGet](https://img.shields.io/nuget/v/cl2j.DataStore.Database-deprecated)](https://www.nuget.org/packages/cl2j.DataStore.Database-deprecated) | Deprecated SQL database repository implementation for cl2j.DataStore, superseded by cl2j.Database. No longer published; the source is in [cl2j.DataStore/cl2j.DataStore.Database-deprecated](cl2j.DataStore/cl2j.DataStore.Database-deprecated). |

Providers share the README of the package they extend.

## Install

```bash
dotnet add package cl2j.Tooling
```

Replace `cl2j.Tooling` with any package id from the table. A package that builds on another one
(for example `cl2j.Logging` on `cl2j.FileStorage`) pulls it in as a dependency.

## Build and test

You need the .NET 10 SDK.

```bash
dotnet build dotnet-toolkit.slnx -c Release
dotnet test dotnet-toolkit.slnx -c Release
```

`cl2j.Database.IntegrationTests` starts a real SQL Server in a container through Testcontainers, so
`dotnet test` on the whole solution needs **Docker running**. Those tests fail, rather than skip,
when Docker is unavailable. See [CONTRIBUTING.md](CONTRIBUTING.md#integration-tests-need-docker).

## Documentation

- [CONTRIBUTING.md](CONTRIBUTING.md): language, tests, CI, versioning and security reporting.
- [docs/nuget-packaging.md](docs/nuget-packaging.md): implementation plan for packing and publishing the packages.
- [docs/nuget-packaging-versioning-design.md](docs/nuget-packaging-versioning-design.md): design of the NuGet packaging and versioning.

## License

[MIT](LICENSE)
