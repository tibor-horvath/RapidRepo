# RapidRepo.EntityFrameworkCore

The Entity Framework Core provider for RapidRepo: repositories and a Unit of Work that implement the `RapidRepo` contracts on top of a `DbContext`.

## Installation

```bash
dotnet add package RapidRepo.EntityFrameworkCore
```

This pulls in `RapidRepo`, which holds the entities and contracts.

## Types

- `EfRepository<TEntity, TId>`: a full repository over `DbContext.Set<TEntity>()`. Derive from it for custom repositories.
- `EfRepository<TEntity, TId, TContext>`: the same, bound to a specific `DbContext` type.
- `EfReadOnlyRepository<TEntity, TId>` / `EfWriteRepository<TEntity, TId>`: read-only and write-only bases.
- `EfUnitOfWork<TUserKey>`: a Unit of Work that stamps audit fields and saves the `DbContext`.
- `EfReadOptions<TEntity>`: pass it wherever a read takes `ReadOptions` for includes, tracking, split queries and query filters.
- `IEfRepository<TEntity, TId>` / `IEfReadOnlyRepository<TEntity, TId>`: the contracts plus `Query()`, which returns an `IQueryable<TEntity>`.
- `ModelBuilder.AddSoftDeleteQueryFilters()`: excludes soft-deleted rows from queries by default.

The 7.x names (`Repository<,>`, `BaseRepository<,>`, `ReadOnlyRepository<,>`, `WriteRepository<,>`, `UnitOfWork<>`) still compile but are obsolete and will be removed in the next major version.
