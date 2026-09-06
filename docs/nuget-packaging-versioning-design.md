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
    <Version>4.0.0</Version>
    <Authors>CL2J Technologies</Authors>
    <Company>CL2J Technologies</Company>
    <RepositoryUrl>https://github.com/CL2J-Technologies/dotnet-toolkit</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
</Project>
```

**To bump the version**: edit `<Version>` in this file and commit to `main`. No `.csproj`
overrides it — see *History* below for what happens when one does.

### 2. Per-library `.csproj` changes

Each publishable project adds to its `<PropertyGroup>`:

```xml
<IsPackable>true</IsPackable>
<PackageId>cl2j.LibraryName</PackageId>
<Description>Short description of the library.</Description>
```

`TestApp`, `Tests`, `Samples` and `Tools` projects inherit `IsPackable=false` and are never packaged.

### 3. GitHub Actions

Two workflows, split in September 2026 (issue #9). `ci.yml` runs on `pull_request` and holds no
secrets; `publish.yml` runs on push to `main` and holds `NUGET_API_KEY`. Before the split there
was only the second one, so nothing ever compiled a branch until the run that published it.

`publish.yml` keeps its own build and test steps: it must never pack an artifact it has not
compiled and tested itself, and ci.yml passing on the pull request does not prove main will.

#### `.github/workflows/publish.yml`

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

Note the failure mode this creates: a version number that is too low does not fail, it publishes
nothing and reports success. Under shared versioning the bump has to clear every previously
published version of every package.

**Prerequisite**: add the `NUGET_API_KEY` secret under _GitHub → Settings → Secrets → Actions_.

## Published Packages

Direct dependencies only, read from the `ProjectReference` entries of each packable project.
Publish order runs down the table: a package is packed after everything it depends on.

| Package                                      | Depends on                       |
| -------------------------------------------- | -------------------------------- |
| `cl2j.Tooling`                               | —                                |
| `cl2j.Smapper`                               | —                                |
| `cl2j.Scripting`                             | cl2j.Tooling                     |
| `cl2j.FileStorage`                           | cl2j.Tooling                     |
| `cl2j.WebTooling`                            | cl2j.Tooling                     |
| `cl2j.Image`                                 | cl2j.FileStorage, cl2j.Tooling   |
| `cl2j.FileStorage.Provider.AzureBlobStorage` | cl2j.FileStorage                 |
| `cl2j.Logging`                               | cl2j.FileStorage                 |
| `cl2j.DataStore`                             | cl2j.FileStorage                 |
| `cl2j.DataStore.Json`                        | cl2j.DataStore                   |
| `cl2j.Database`                              | cl2j.Scripting, cl2j.Tooling     |
| `cl2j.Database.SqlServer`                    | cl2j.Database                    |
| `cl2j.DataStore.Database-deprecated`         | —                                |

Under shared versioning the order does not need managing by hand — one `dotnet pack` over the
solution publishes the set together, and every floor lands on the same number. The table matters
when reading a published `.nuspec`, and it mattered a great deal while versions were per package.

`dotnet pack` automatically converts `<ProjectReference>` entries to `<PackageReference>` in the generated `.nuspec`, using the shared version.

## Not Packaged

- All `TestApp`, `Tests`, `Samples`, `Tools` projects

`cl2j.DataStore.Database-deprecated` was listed here, but it sets `IsPackable=true` and has been
published all along. It is not dead either: cooperius depends on it. The name is the only thing
deprecated about it.

## Release Workflow

1. Edit `<Version>` in `Directory.Build.props`
2. Commit and push to `main`
3. GitHub Actions pipeline builds → tests → packs → pushes automatically
4. Packages appear on NuGet.org within a few minutes

## History

**September 2026 — per-package versions, and back.** The repository drifted away from shared
versioning: packages that changed were given their own `<Version>` in their `.csproj`
(`cl2j.FileStorage` 2.2.0, `cl2j.Image` 3.1.0, `cl2j.Database` 3.0.0) while
`Directory.Build.props` stayed at 2.0.0. The motivation was reasonable on its face — publish only
what changed, and give each package a version that means something.

It broke dependency floors. `dotnet pack` converts each `ProjectReference` into a
`PackageReference` at the referenced project's version, so a package that is not republished keeps
pointing at whatever its dependency was when it was last packed. `cl2j.Logging` 2.0.0 kept
depending on `cl2j.FileStorage` 2.0.0 long after 2.2.0 shipped, so a consumer referencing
`cl2j.Logging` silently resolved a version missing two data-loss fixes. Four packages were stale
before anyone checked: `cl2j.DataStore`, `cl2j.FileStorage.Provider.AzureBlobStorage`,
`cl2j.Logging` at 2.0.0, and `cl2j.Image` at 2.1.1.

Shared versioning was restored at **4.0.0**, the first number clearing every version published
during that period. Issue #17 has the measurements.

The lesson worth keeping: the cost of shared versioning is visible — packages get numbers they did
not earn. The cost of per-package versioning is invisible — consumers get dependencies they did
not ask for. A visible cost is the cheaper one.
