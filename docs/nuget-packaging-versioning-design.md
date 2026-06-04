# Design: NuGet Package Generation with Versioning

## Context

This repository contains ~12 .NET libraries (net10.0) consumed by external projects. The goal is to publish these libraries to NuGet.org with shared versioning, triggered automatically on every push to `main`.

## Key Decisions

| Dimension            | Decision                                             |
| -------------------- | ---------------------------------------------------- |
| Destination          | NuGet.org (public)                                   |
| Versioning strategy  | Shared — all libs publish the same version           |
| Trigger              | Push to `main`                                       |
| Version bump control | Manual — edit `<Version>` in `Directory.Build.props` |

## Architecture

### 1. `Directory.Build.props` (root)

Single source of truth for metadata shared across all projects.

```xml
<Project>
  <PropertyGroup>
    <Version>1.0.0</Version>
    <Authors>CL2J Technologies</Authors>
    <Company>CL2J Technologies</Company>
    <RepositoryUrl>https://github.com/CL2J-Technologies/dotnet-toolkit</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
</Project>
```

**To bump the version**: edit `<Version>` in this file and commit to `main`.

### 2. Per-library `.csproj` changes

Each publishable project adds to its `<PropertyGroup>`:

```xml
<IsPackable>true</IsPackable>
<PackageId>cl2j.LibraryName</PackageId>
<Description>Short description of the library.</Description>
```

`TestApp`, `Tests`, `Samples` and `Tools` projects inherit `IsPackable=false` and are never packaged.

### 3. GitHub Actions — `.github/workflows/publish.yml`

```yaml
name: Publish NuGet Packages

on:
  push:
    branches: [main]

jobs:
  publish:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "10.x"

      - run: dotnet restore
      - run: dotnet build --no-restore -c Release
      - run: dotnet test --no-build -c Release

      - name: Pack
        run: dotnet pack --no-build -c Release -o ./artifacts

      - name: Push to NuGet.org
        run: dotnet nuget push ./artifacts/*.nupkg --api-key ${{ secrets.NUGET_API_KEY }} --source https://api.nuget.org/v3/index.json --skip-duplicate
    env:
      DOTNET_NOLOGO: true
```

**`--skip-duplicate`**: if the version already exists on NuGet.org, the push is silently skipped. A push to `main` without a version bump publishes nothing.

**Prerequisite**: add the `NUGET_API_KEY` secret under _GitHub → Settings → Secrets → Actions_.

## Published Packages

| Package                                      | Depends on       |
| -------------------------------------------- | ---------------- |
| `cl2j.Tooling`                               | —                |
| `cl2j.Smapper`                               | —                |
| `cl2j.Image`                                 | —                |
| `cl2j.FileStorage`                           | cl2j.Tooling     |
| `cl2j.FileStorage.Provider.AzureBlobStorage` | cl2j.FileStorage |
| `cl2j.Logging`                               | cl2j.FileStorage |
| `cl2j.DataStore`                             | cl2j.FileStorage |
| `cl2j.DataStore.Json`                        | cl2j.DataStore   |
| `cl2j.Database`                              | —                |
| `cl2j.Database.SqlServer`                    | cl2j.Database    |
| `cl2j.Scripting`                             | —                |
| `cl2j.WebTooling`                            | —                |

`dotnet pack` automatically converts `<ProjectReference>` entries to `<PackageReference>` in the generated `.nuspec`, using the shared version.

## Not Packaged

- `cl2j.DataStore.Database-deprecated`
- All `TestApp`, `Tests`, `Samples`, `Tools` projects

## Release Workflow

1. Edit `<Version>` in `Directory.Build.props`
2. Commit and push to `main`
3. GitHub Actions pipeline builds → tests → packs → pushes automatically
4. Packages appear on NuGet.org within a few minutes
