# NuGet Package Generation with Versioning — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Publish all 12 cl2j libraries to NuGet.org automatically on every push to `main`, using a shared version controlled via a single `Directory.Build.props` file.

**Architecture:** A root `Directory.Build.props` centralises shared build properties and package metadata; each library `.csproj` opts in with `<IsPackable>true</IsPackable>` and its own `<PackageId>` / `<Description>`. A GitHub Actions workflow builds, tests, packs, and pushes on every push to `main`, skipping duplicate versions silently.

**Tech Stack:** .NET 10, MSBuild `Directory.Build.props`, `dotnet pack`, GitHub Actions, NuGet.org.

---

## File Map

**Create:**
- `Directory.Build.props` — shared build + packaging metadata, `IsPackable=false` default
- `.github/workflows/publish.yml` — CI/CD pipeline

**Modify (add packaging metadata, remove now-redundant shared properties):**
- `cl2j.Tooling/cl2j.Tooling.csproj`
- `cl2j.Smapper/cl2j.Smapper.csproj`
- `cl2j.Image/cl2j.Image.csproj`
- `cl2j.Scripting/cl2j.Scripting/cl2j.Scripting.csproj`
- `cl2j.WebTooling/cl2j.WebTooling.csproj`
- `cl2j.FileStorage/cl2j.FileStorage/cl2j.FileStorage.csproj`
- `cl2j.FileStorage/cl2j.FileStorage.Provider.AzureBlobStorage/cl2j.FileStorage.Provider.AzureBlobStorage.csproj`
- `cl2j.Logging/cl2j.Logging/cl2j.Logging.csproj`
- `cl2j.DataStore/cl2j.DataStore/cl2j.DataStore.csproj`
- `cl2j.DataStore/cl2j.DataStore.Json/cl2j.DataStore.Json.csproj`
- `cl2j.Database/cl2j.Database/cl2j.Database.csproj`
- `cl2j.Database/cl2j.Database.SqlServer/cl2j.Database.SqlServer.csproj`

---

## Task 1: Create `Directory.Build.props`

**Files:**
- Create: `Directory.Build.props`

- [ ] **Step 1: Create the file**

Full content of `Directory.Build.props` (repo root):

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AnalysisMode>Recommended</AnalysisMode>

    <Version>1.0.0</Version>
    <Authors>CL2J Technologies</Authors>
    <Company>CL2J Technologies</Company>
    <RepositoryUrl>https://github.com/CL2J-Technologies/dotnet-toolkit</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <EditorConfigFiles Remove=".editorconfig" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Verify build still passes**

```bash
dotnet build -c Release
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
git add Directory.Build.props
git commit -m "build: add Directory.Build.props with shared metadata and packaging defaults"
```

---

## Task 2: Update Foundation Library Projects

Updates `cl2j.Tooling`, `cl2j.Smapper`, `cl2j.Image`, `cl2j.Scripting`, and `cl2j.WebTooling`. These have no inter-library dependencies on other cl2j packages.

**Files:**
- Modify: `cl2j.Tooling/cl2j.Tooling.csproj`
- Modify: `cl2j.Smapper/cl2j.Smapper.csproj`
- Modify: `cl2j.Image/cl2j.Image.csproj`
- Modify: `cl2j.Scripting/cl2j.Scripting/cl2j.Scripting.csproj`
- Modify: `cl2j.WebTooling/cl2j.WebTooling.csproj`

- [ ] **Step 1: Replace `cl2j.Tooling/cl2j.Tooling.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>true</IsPackable>
    <PackageId>cl2j.Tooling</PackageId>
    <Description>General-purpose .NET utilities: caching, compression, configuration helpers, and DI extensions.</Description>
  </PropertyGroup>

  <ItemGroup>
    <None Include="README.md" Pack="true" PackagePath="\" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Configuration.Abstractions" Version="10.0.3" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Binder" Version="10.0.3" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.3" />
    <PackageReference Include="Microsoft.Extensions.Options" Version="10.0.3" />
    <PackageReference Include="Newtonsoft.Json" Version="13.0.4" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Replace `cl2j.Smapper/cl2j.Smapper.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>true</IsPackable>
    <PackageId>cl2j.Smapper</PackageId>
    <Description>Lightweight reflection-based object-to-object mapper.</Description>
    <AssemblyName>cl2j.Smapper</AssemblyName>
    <RootNamespace>cl2j.Smapper</RootNamespace>
  </PropertyGroup>
</Project>
```

- [ ] **Step 3: Replace `cl2j.Image/cl2j.Image.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>true</IsPackable>
    <PackageId>cl2j.Image</PackageId>
    <Description>Image utilities: EXIF data extraction, image comparison, and resizing.</Description>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="SixLabors.ImageSharp" Version="3.1.12" />
    <PackageReference Include="System.Drawing.Common" Version="10.0.3" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\cl2j.FileStorage\cl2j.FileStorage\cl2j.FileStorage.csproj" />
    <ProjectReference Include="..\cl2j.Tooling\cl2j.Tooling.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Replace `cl2j.Scripting/cl2j.Scripting/cl2j.Scripting.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>true</IsPackable>
    <PackageId>cl2j.Scripting</PackageId>
    <Description>Dynamic C# code compilation and execution at runtime.</Description>
  </PropertyGroup>

  <ItemGroup>
    <None Include="..\README.md" Pack="true" PackagePath="\" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="5.0.0" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.3" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\cl2j.Tooling\cl2j.Tooling.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 5: Replace `cl2j.WebTooling/cl2j.WebTooling.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>true</IsPackable>
    <PackageId>cl2j.WebTooling</PackageId>
    <Description>ASP.NET Core utilities: Claims, HttpClient, and HttpRequest extensions.</Description>
  </PropertyGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\cl2j.Tooling\cl2j.Tooling.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 6: Verify build passes**

```bash
dotnet build -c Release
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 7: Commit**

```bash
git add cl2j.Tooling/cl2j.Tooling.csproj cl2j.Smapper/cl2j.Smapper.csproj cl2j.Image/cl2j.Image.csproj cl2j.Scripting/cl2j.Scripting/cl2j.Scripting.csproj cl2j.WebTooling/cl2j.WebTooling.csproj
git commit -m "build: enable NuGet packaging for Tooling, Smapper, Image, Scripting, WebTooling"
```

---

## Task 3: Update FileStorage Library Projects

**Files:**
- Modify: `cl2j.FileStorage/cl2j.FileStorage/cl2j.FileStorage.csproj`
- Modify: `cl2j.FileStorage/cl2j.FileStorage.Provider.AzureBlobStorage/cl2j.FileStorage.Provider.AzureBlobStorage.csproj`

- [ ] **Step 1: Replace `cl2j.FileStorage/cl2j.FileStorage/cl2j.FileStorage.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>true</IsPackable>
    <PackageId>cl2j.FileStorage</PackageId>
    <Description>Multi-provider file storage abstraction: read, write, delete, and more. Extensible via Dependency Injection.</Description>
  </PropertyGroup>

  <ItemGroup>
    <None Include="..\README.md" Pack="true" PackagePath="\" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Configuration.Abstractions" Version="10.0.3" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Binder" Version="10.0.3" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.3" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\cl2j.Tooling\cl2j.Tooling.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Replace `cl2j.FileStorage/cl2j.FileStorage.Provider.AzureBlobStorage/cl2j.FileStorage.Provider.AzureBlobStorage.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>true</IsPackable>
    <PackageId>cl2j.FileStorage.Provider.AzureBlobStorage</PackageId>
    <Description>Azure Blob Storage provider for cl2j.FileStorage.</Description>
  </PropertyGroup>

  <ItemGroup>
    <None Include="..\README.md" Pack="true" PackagePath="\" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Azure.Storage.Blobs" Version="12.26.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\cl2j.FileStorage\cl2j.FileStorage.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Verify build passes**

```bash
dotnet build -c Release
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 4: Commit**

```bash
git add "cl2j.FileStorage/cl2j.FileStorage/cl2j.FileStorage.csproj" "cl2j.FileStorage/cl2j.FileStorage.Provider.AzureBlobStorage/cl2j.FileStorage.Provider.AzureBlobStorage.csproj"
git commit -m "build: enable NuGet packaging for FileStorage and AzureBlobStorage provider"
```

---

## Task 4: Update Logging Library Project

**Files:**
- Modify: `cl2j.Logging/cl2j.Logging/cl2j.Logging.csproj`

- [ ] **Step 1: Replace `cl2j.Logging/cl2j.Logging/cl2j.Logging.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>true</IsPackable>
    <PackageId>cl2j.Logging</PackageId>
    <Description>Logging extensions and unhandled exception handling for ASP.NET Core applications.</Description>
  </PropertyGroup>

  <ItemGroup>
    <None Include="..\README.md" Pack="true" PackagePath="\" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="10.0.3" />
    <PackageReference Include="Microsoft.Extensions.Logging" Version="10.0.3" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\cl2j.FileStorage\cl2j.FileStorage\cl2j.FileStorage.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Verify build passes**

```bash
dotnet build -c Release
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
git add cl2j.Logging/cl2j.Logging/cl2j.Logging.csproj
git commit -m "build: enable NuGet packaging for Logging"
```

---

## Task 5: Update DataStore Library Projects

**Files:**
- Modify: `cl2j.DataStore/cl2j.DataStore/cl2j.DataStore.csproj`
- Modify: `cl2j.DataStore/cl2j.DataStore.Json/cl2j.DataStore.Json.csproj`

- [ ] **Step 1: Replace `cl2j.DataStore/cl2j.DataStore/cl2j.DataStore.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>true</IsPackable>
    <PackageId>cl2j.DataStore</PackageId>
    <Description>Multi-provider CRUD repository abstraction. Extensible via Dependency Injection.</Description>
  </PropertyGroup>

  <ItemGroup>
    <None Include="..\README.md" Pack="true" PackagePath="\" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\cl2j.FileStorage\cl2j.FileStorage\cl2j.FileStorage.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Replace `cl2j.DataStore/cl2j.DataStore.Json/cl2j.DataStore.Json.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>true</IsPackable>
    <PackageId>cl2j.DataStore.Json</PackageId>
    <Description>JSON file provider for cl2j.DataStore.</Description>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\cl2j.DataStore\cl2j.DataStore.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Verify build passes**

```bash
dotnet build -c Release
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 4: Commit**

```bash
git add "cl2j.DataStore/cl2j.DataStore/cl2j.DataStore.csproj" "cl2j.DataStore/cl2j.DataStore.Json/cl2j.DataStore.Json.csproj"
git commit -m "build: enable NuGet packaging for DataStore and DataStore.Json"
```

---

## Task 6: Update Database Library Projects

**Files:**
- Modify: `cl2j.Database/cl2j.Database/cl2j.Database.csproj`
- Modify: `cl2j.Database/cl2j.Database.SqlServer/cl2j.Database.SqlServer.csproj`

- [ ] **Step 1: Replace `cl2j.Database/cl2j.Database/cl2j.Database.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>true</IsPackable>
    <PackageId>cl2j.Database</PackageId>
    <Description>Database abstraction layer with connection and query extensions.</Description>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Hosting.Abstractions" Version="10.0.3" />
    <PackageReference Include="Microsoft.Extensions.Logging" Version="10.0.3" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\cl2j.Scripting\cl2j.Scripting\cl2j.Scripting.csproj" />
    <ProjectReference Include="..\..\cl2j.Tooling\cl2j.Tooling.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Replace `cl2j.Database/cl2j.Database.SqlServer/cl2j.Database.SqlServer.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>true</IsPackable>
    <PackageId>cl2j.Database.SqlServer</PackageId>
    <Description>SQL Server provider for cl2j.Database.</Description>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Data.SqlClient" Version="6.0.1" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\cl2j.Database\cl2j.Database.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Verify build passes**

```bash
dotnet build -c Release
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 4: Commit**

```bash
git add "cl2j.Database/cl2j.Database/cl2j.Database.csproj" "cl2j.Database/cl2j.Database.SqlServer/cl2j.Database.SqlServer.csproj"
git commit -m "build: enable NuGet packaging for Database and Database.SqlServer"
```

---

## Task 7: Verify Pack Output

Confirms that exactly 12 `.nupkg` files are produced and no test/sample projects sneak in.

**Files:** none

- [ ] **Step 1: Pack the solution**

```bash
dotnet pack -c Release -o ./artifacts
```

- [ ] **Step 2: Verify exactly 12 packages are produced**

```bash
ls ./artifacts/*.nupkg
```

Expected (order may vary):
```
cl2j.Database.1.0.0.nupkg
cl2j.Database.SqlServer.1.0.0.nupkg
cl2j.DataStore.1.0.0.nupkg
cl2j.DataStore.Json.1.0.0.nupkg
cl2j.FileStorage.1.0.0.nupkg
cl2j.FileStorage.Provider.AzureBlobStorage.1.0.0.nupkg
cl2j.Image.1.0.0.nupkg
cl2j.Logging.1.0.0.nupkg
cl2j.Scripting.1.0.0.nupkg
cl2j.Smapper.1.0.0.nupkg
cl2j.Tooling.1.0.0.nupkg
cl2j.WebTooling.1.0.0.nupkg
```

If a package is missing, check that the corresponding `.csproj` has `<IsPackable>true</IsPackable>`.  
If an unexpected package appears (e.g. a TestApp), check that the project does **not** have `<IsPackable>true</IsPackable>`.

- [ ] **Step 3: Clean up artifacts folder**

```bash
rm -rf ./artifacts
```

- [ ] **Step 4: Add `artifacts/` to `.gitignore`**

Add this line to `.gitignore` (create the file if it doesn't exist):
```
artifacts/
```

```bash
git add .gitignore
git commit -m "chore: add artifacts/ to .gitignore"
```

---

## Task 8: Create GitHub Actions Workflow

**Files:**
- Create: `.github/workflows/publish.yml`

- [ ] **Step 1: Create the directory and workflow file**

```bash
mkdir -p .github/workflows
```

Full content of `.github/workflows/publish.yml`:

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
          dotnet-version: '10.x'

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

- [ ] **Step 2: Commit**

```bash
git add .github/workflows/publish.yml
git commit -m "ci: add GitHub Actions workflow to publish NuGet packages on push to main"
```

---

## Task 9: Add NuGet API Key Secret (Manual Step)

This step is performed in the GitHub web UI — not via code.

- [ ] **Step 1: Generate a NuGet.org API key**

1. Log in to [nuget.org](https://www.nuget.org)
2. Go to **Account → API Keys**
3. Click **Create**
4. Set **Key Name**: `dotnet-toolkit-github-actions`
5. Set **Expiration**: 365 days (or your preference)
6. Set **Glob pattern**: `cl2j.*`
7. Click **Create** and copy the key immediately (shown only once)

- [ ] **Step 2: Add the secret to GitHub**

1. Go to the repository on GitHub
2. Navigate to **Settings → Secrets and variables → Actions**
3. Click **New repository secret**
4. Set **Name**: `NUGET_API_KEY`
5. Paste the key from Step 1
6. Click **Add secret**

- [ ] **Step 3: Trigger the first publish**

Push any commit to `main` (or bump the version and push). The workflow will run automatically. Monitor it under **Actions** on GitHub.

To verify success, search for `cl2j.Tooling` on [nuget.org](https://www.nuget.org) after the workflow completes.

---

## Release Workflow (ongoing)

To publish a new version after this setup is complete:

1. Edit `<Version>` in `Directory.Build.props`
2. Commit and push to `main`
3. GitHub Actions builds, tests, packs, and pushes automatically
4. Packages appear on NuGet.org within minutes
