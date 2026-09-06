# Contributing

## Language

**Everything written in this repository is in English.** That covers:

- Source code — identifiers, comments, XML documentation, test names
- Documentation — `README.md`, everything under `docs/`, this file
- Commit messages — subject and body
- Pull requests — titles and descriptions
- Issues — titles, bodies, and comments

The repository is public and its packages are published to NuGet.org. A consumer reading an
exception message, an XML doc tooltip, or an issue thread should not need a second language to
follow it.

### Existing French comments

Parts of the codebase still carry French comments, written before this rule. Two ways they go:

- **Opportunistically.** If you are editing a method whose comments are in French, translate them
  as part of your change.
- **In a sweep.** A deliberate translation pass is fine, but give it its own pull request, separate
  from any behaviour change — mixed in, it buries the part a reviewer needs to read.

`cl2j.Database` is done. Still outstanding: `cl2j.FileStorage`, `cl2j.Image`, `cl2j.Logging`,
`cl2j.WebTooling`.

## Tests

New behaviour and bug fixes come with tests, and the test is written before the fix. A test written
afterwards passes immediately, which proves that it runs — not that it would have caught anything.

Test projects must be registered in `dotnet-toolkit.slnx`. A test project outside the solution is
never built and never run — `cl2j.Scripting.Tests` and `cl2j.FileStorage.Tests` were in exactly
that state and had never executed in CI.

### Integration tests need Docker

`cl2j.Database.IntegrationTests` starts a real SQL Server in a container, through Testcontainers.
`dotnet test` on the solution therefore needs Docker running. Install Docker Desktop and it works
with no further configuration; the CI runner already has it.

Those tests **fail** rather than skip when Docker is unavailable, on purpose. A skipped test reads
as a passing one in a summary line, and the skip becomes permanent — which is how a suite ends up
green without having exercised anything.

They exist because everything in `cl2j.Database.Tests` stops at the text of a statement. The
sixteen public operations on `ConnectionExtensions`, and the whole of `DbReaderExtensions`, are
only reachable with a server.

## CI

`ci.yml` builds and tests every pull request on `ubuntu-latest`, and is a required check on
`main`: a red branch cannot merge.

That check is the point of the workflow, not a formality. It was added in September 2026 because
`publish.yml` triggered only on push to `main`, so the first run that ever compiled a branch was
the run that published it to NuGet.org — a failure arrived after the merge, during a release.

Linux is deliberate. `cl2j.Image` shipped for fifteen months unable to produce a single thumbnail
off Windows, and nothing ever built it on Linux to notice.

## Versioning

**Every package publishes the same version**, held in `<Version>` in `Directory.Build.props` at the
repository root. To release: bump that one number, commit to `main`, and the workflow publishes the
whole set together.

**No `.csproj` declares its own `<Version>`.** A local override is not a shortcut, it is the bug —
see below.

### Why shared, and not per package

`dotnet pack` turns each `ProjectReference` into a `PackageReference` at the referenced project's
version. A package that is not republished therefore keeps pointing at whatever its dependency was
on the day it was last packed.

The repository ran on per-package versions for a while, and that is exactly what happened:
`cl2j.Logging` 2.0.0 kept depending on `cl2j.FileStorage` **2.0.0** while 2.2.0 was out — two
fixes behind, both of them data-loss bugs, with nothing anywhere to signal it. A consumer that
referenced `cl2j.Logging` and did the obvious thing silently got the broken version. Four packages
were in that state before anyone looked. See issue #17.

Publishing everything together means those floors are always current. That is the point of the
scheme, not a side effect of it.

### The cost, accepted knowingly

A package that did not change still gets a new number. The version therefore says *which release a
package belongs to*, not *what changed inside it* — a consumer cannot read semver meaning into a
bump of a package that was merely carried along.

Do not "fix" this by giving one package its own version. That reintroduces the stale floors, and
it does so silently, which is the part that makes it expensive.

### Choosing the number

A breaking API change anywhere in the repository makes the shared bump a major one. Removing a
member from a published interface is breaking, even when the only implementation here is internal.

The number must exceed every version already published, for every package. `--skip-duplicate` in
the workflow means a number that already exists on NuGet.org is skipped in silence rather than
failing, so a too-low number does not publish and does not complain either.

## Security

Do not open a public issue for a vulnerability. Use
[Security Advisories](https://github.com/CL2J-Technologies/dotnet-toolkit/security/advisories/new)
so the fix can ship before the mechanism is described publicly.
