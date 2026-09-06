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
never built and never run.

## Versioning

Packages carry their own `<Version>`, bumped manually. `Directory.Build.props` at the repository
root supplies the default for packages that have not been versioned individually.

`cl2j.Database` and `cl2j.Database.SqlServer` version together, from
`cl2j.Database/Directory.Build.props`. Neither project declares its own `<Version>`.

Breaking a public API means a major bump. Removing a member from a published interface is breaking,
even when the only implementation in this repository is internal.

## Security

Do not open a public issue for a vulnerability. Use
[Security Advisories](https://github.com/CL2J-Technologies/dotnet-toolkit/security/advisories/new)
so the fix can ship before the mechanism is described publicly.
