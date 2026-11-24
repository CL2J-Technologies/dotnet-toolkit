# dotnet-toolkit

A collection of small, independent .NET libraries from CL2J Technologies: data access, file storage,
caching, logging, image handling, object mapping, runtime C# compilation and ASP.NET Core helpers.
Each library is published to NuGet.org as its own package, so you reference only what you use.

All packages target **net10.0** and share a single version number: every release publishes the whole
set together (see [Versioning](CONTRIBUTING.md#versioning)).

## Packages

| Package | NuGet | Description |
|---|---|---|
| [cl2j.Database](cl2j.Database/README.md) | [![NuGet](https://img.shields.io/nuget/v/cl2j.Database)](https://www.nuget.org/packages/cl2j.Database) | A fast, attribute-driven micro-ORM for SQL Server: describe a table with a plain class and get CRUD, queries by key or by example, generated string keys, JSON columns, bulk insert and table creation, all derived from the class. Dapper-level speed, no DbContext, a fraction of EF Core's memory. **Provider:** [cl2j.Database.SqlServer](https://www.nuget.org/packages/cl2j.Database.SqlServer) (required). |
| [cl2j.FileStorage](cl2j.FileStorage/README.md) | [![NuGet](https://img.shields.io/nuget/v/cl2j.FileStorage)](https://www.nuget.org/packages/cl2j.FileStorage) | One file API for local disk, Azure Blob Storage and S3-compatible storage. Switch the provider in appsettings.json, not in your code. **Providers:** local disk (built in), [cl2j.FileStorage.Provider.AzureBlobStorage](https://www.nuget.org/packages/cl2j.FileStorage.Provider.AzureBlobStorage), [cl2j.FileStorage.Provider.S3](https://www.nuget.org/packages/cl2j.FileStorage.Provider.S3) (Amazon S3, Cloudflare R2, MinIO). |
| [cl2j.DataStore](cl2j.DataStore/README.md) | [![NuGet](https://img.shields.io/nuget/v/cl2j.DataStore)](https://www.nuget.org/packages/cl2j.DataStore) | CRUD repositories behind one interface, with a memory-cache decorator. Swap the backing store through dependency injection. **Provider:** [cl2j.DataStore.Json](https://www.nuget.org/packages/cl2j.DataStore.Json). |
| [cl2j.Logging](cl2j.Logging/README.md) | [![NuGet](https://img.shields.io/nuget/v/cl2j.Logging)](https://www.nuget.org/packages/cl2j.Logging) | File logging for Microsoft.Extensions.Logging, to disk, Azure Blob or S3 through cl2j.FileStorage: buffered writes, daily and size-based rollover, timestamps in the time zone you choose. |
| [cl2j.Image](cl2j.Image/README.md) | [![NuGet](https://img.shields.io/nuget/v/cl2j.Image)](https://www.nuget.org/packages/cl2j.Image) | Make uploaded photos web-ready in one call: upright from EXIF, camera and GPS metadata stripped, resized, re-encoded to JPEG or WebP. Thumbnails and duplicate detection included. Cross-platform, on SkiaSharp. |
| [cl2j.Scripting](cl2j.Scripting/README.md) | [![NuGet](https://img.shields.io/nuget/v/cl2j.Scripting)](https://www.nuget.org/packages/cl2j.Scripting) | Compile C# snippets once at runtime with Roslyn and call them like regular methods, with a typed input and result and readable compiler errors. |
| [cl2j.Smapper](cl2j.Smapper/README.md) | [![NuGet](https://img.shields.io/nuget/v/cl2j.Smapper)](https://www.nuget.org/packages/cl2j.Smapper) | Zero-configuration object-to-object mapper: copies same-name, same-type properties and deep-clones through JSON. One extension method, nothing to register. |
| [cl2j.WebTooling](cl2j.WebTooling/README.md) | [![NuGet](https://img.shields.io/nuget/v/cl2j.WebTooling)](https://www.nuget.org/packages/cl2j.WebTooling) | ASP.NET Core one-liners: claims, client IP and URL, language selection, and posting uploaded files with HttpClient. Nothing to configure. |
| [cl2j.Tooling](cl2j.Tooling/README.md) | [![NuGet](https://img.shields.io/nuget/v/cl2j.Tooling)](https://www.nuget.org/packages/cl2j.Tooling) | Everyday .NET building blocks: a self-refreshing cache, localized values, an SMTP email service, a small JSON REST client, common exception types, and string, stream and type helpers. |

Providers are separate packages and share the README of the library they extend.
`cl2j.DataStore.Database-deprecated` is superseded by `cl2j.Database` and no longer published; its source is in
[cl2j.DataStore/cl2j.DataStore.Database-deprecated](cl2j.DataStore/cl2j.DataStore.Database-deprecated).

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
