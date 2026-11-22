# RapidRepo documentation

New to RapidRepo? Start with the [Quick Start](../README.md#quick-start), then read **Entities**, **Repositories** and **Unit of Work** in order.

## Core concepts (`RapidRepo`)

| Page | What it covers |
|---|---|
| [Entities](RapidRepo/entities.md) | Base classes and choosing one |
| [Repositories](RapidRepo/repositories.md) | Repository interfaces, how writes behave, custom methods |
| [Unit of Work](RapidRepo/unit-of-work.md) | Defining and implementing a unit of work, `GetRepository`, committing |
| [Querying](RapidRepo/querying.md) | Filtering, sorting, projection, paging, bulk operations |

## Features

| Page | What it covers |
|---|---|
| [Soft Delete](RapidRepo/soft-delete.md) | `IDeletableEntity`, reading deleted rows, restore, permanent removal |
| [Auditing](RapidRepo/auditing.md) | Audit stamping rules, the current user, strict mode, the clock |
| [Optimistic Concurrency](RapidRepo/concurrency.md) | `IVersionedEntity`, updates from a client, handling conflicts |

## Reference

| Page | What it covers |
|---|---|
| [API Reference](RapidRepo/api-reference.md) | Every read and write method, `ReadOptions`, `SortOrder<T>`, `Paged<T>` |
| [Writing a provider](RapidRepo/providers.md) | What a new provider implements, and the conformance suite |

## Packages

| Package | Page | What it covers |
|---|---|---|
| `RapidRepo.EntityFrameworkCore` | [EF Core](RapidRepo.EntityFrameworkCore/entity-framework-core.md) | `EfRepository`, `EfUnitOfWork`, `EfReadOptions`, custom queries, query filters, concurrency tokens |
| `RapidRepo.EntityFrameworkCore` | [EF Core registration](RapidRepo.EntityFrameworkCore/ef-core-registration.md) | `UseEntityFrameworkCore`, generic repositories per `DbSet`, multiple contexts |
| `RapidRepo.Extensions.DependencyInjection` | [Dependency Injection](RapidRepo.Extensions.DependencyInjection/dependency-injection.md) | `AddRapidRepo`, scanning, filters, lifetime, the current user, providers |
| `RapidRepo.SourceGenerators` | [Generated Unit of Work](RapidRepo.SourceGenerators/generate-unit-of-work.md) | `[GenerateUnitOfWork]`, attribute reference, diagnostics |

## Upgrading

- [Migrating from 8.x to 9.0](migration/8.x-to-9.0.md)
