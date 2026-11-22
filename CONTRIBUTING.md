# Contributing to RapidRepo

Thanks for your interest in RapidRepo. Bug reports, documentation fixes and pull requests are all welcome.

## Before you start

- **Bugs:** [open an issue](https://github.com/tibor-horvath/RapidRepo/issues/new/choose) with a minimal reproduction: the entity, the call and what you expected.
- **Features and behaviour changes:** open an issue first so the design can be agreed before you write code. RapidRepo's behaviour is a contract that every provider must follow, so changes to it need more thought than the code alone suggests.
- **Security issues:** don't open a public issue; see [SECURITY.md](SECURITY.md).

## Building and testing

You need the .NET 11 SDK. The libraries target .NET 10 and .NET 11.

```bash
dotnet build RapidRepo.sln
dotnet test RapidRepo.sln
```

## Repository layout

| Project | Purpose |
|---|---|
| `RapidRepo` | Provider-neutral contracts and base classes. Must stay free of dependencies |
| `RapidRepo.EntityFrameworkCore` | The EF Core provider |
| `RapidRepo.Extensions.DependencyInjection` | `AddRapidRepo` and assembly scanning |
| `RapidRepo.SourceGenerators` | The `[GenerateUnitOfWork]` source generator |
| `RapidRepo.Testing.Conformance` | Provider-neutral xUnit tests that define the contract (not published) |
| `*.Tests` | Tests for each project; `RapidRepo.EntityFrameworkCore.Tests/Conformance` runs the conformance suite against EF Core |

## Guidelines for changes

- **Behaviour belongs in the conformance suite.** If a change affects write, soft-delete, audit, concurrency, transaction or query behaviour, add or update a test in `RapidRepo.Testing.Conformance` so every provider is held to it, not only EF Core.
- **Keep the contracts provider-neutral.** No EF Core (or other store) types in the `RapidRepo` package.
- **Update the docs.** Public API and behaviour changes need a matching change under [`docs/`](docs/README.md), and XML doc comments on public members.
- **Breaking changes** need a note in the [migration guide](docs/migration/) and a `!` in the PR title (see below).

## Pull requests

PR titles must follow [Conventional Commits](https://www.conventionalcommits.org/), because they become the squash-merge commit and drive [release-please](https://github.com/googleapis/release-please) versioning and the [changelog](CHANGELOG.md). A CI check enforces this. Examples:

```
feat(uow): add explicit transaction support to IUnitOfWork
fix(ef): keep soft-delete filter on derived entities
docs(readme): add an at-a-glance example
feat(repo)!: rename GetAllPagedAsync pageIndex to page
```

Keep PRs focused on one change, and make sure `dotnet test` passes before you request a review.

## Code of conduct

This project follows its [Code of Conduct](CODE_OF_CONDUCT.md). By taking part, you agree to follow it.

## License

By contributing, you agree that your contributions are licensed under the project's [MIT License](LICENSE).
