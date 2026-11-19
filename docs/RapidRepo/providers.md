# Writing a provider

RapidRepo's core package defines the contracts and the rules; a provider implements them for one persistence technology. `RapidRepo.EntityFrameworkCore` is the reference provider. This page lists what a new one, for example a Dapper provider, has to supply and what it gets from the core for free.

---

## What the core gives you

| Type | Namespace | What it holds |
|---|---|---|
| `IReadOnlyRepository<,>`, `IWriteRepository<,>`, `IRepository<,>`, `IUnitOfWork<>` | `RapidRepo.Repositories.Interfaces`, `RapidRepo.UnitOfWork` | The contracts application code depends on |
| `ReadOptions`, `SortOrder<T>` | `RapidRepo.Repositories` | Read options and a declarative sort order, both inspectable |
| `Paging` | `RapidRepo.Repositories` | Page validation, the page offset, the default page order and building `Paged<T>` |
| `WriteRepositoryBase<TEntity, TKey>` | `RapidRepo.Repositories` | Soft vs. hard delete, restore, argument guards and error messages |
| `UnitOfWorkBase<TUserKey>` | `RapidRepo.UnitOfWork` | `Commit`/`CommitAsync`, `GetRepository`, `ResolveRepository`, and the auditor |
| `AuditStamper<TUserKey>`, `EntityChangeKind` | `RapidRepo.Auditing` | The audit rules, applied per entity and change kind |
| `EntityTraits<TEntity>` | `RapidRepo.Entities` | Soft-delete support and a compiled `DeletedBy` setter, cached per entity type |

The core has no package dependencies. An architecture test keeps it free of EF Core.

---

## Repositories

### Writes

Derive from `WriteRepositoryBase<TEntity, TKey>` and implement its staging hooks:

| Hook | Called for |
|---|---|
| `StageInsert` / `StageInsertAsync` | `Add`, `AddAsync` |
| `StageUpdate` | `Update` |
| `StageRemove` | `HardDelete`, and `Delete` of a non-deletable entity |
| `StageDeletionChange(entity, kind, applyChange)` | Soft delete and restore. Call `applyChange` where your provider can observe the change; it sets or clears the deletion marks |
| `FindForWrite` / `FindForWriteAsync` | The by-id writes. Return the entity, including one staged for insertion, and honour `includeDeleted` |
| `StageInsertRange`, `StageUpdateRange`, `StageRemoveRange` | Optional; override to batch |

A change-tracking provider starts tracking the entity in `StageDeletionChange` before calling `applyChange`. A command-based provider calls `applyChange` and queues an update of the given `EntityChangeKind`.

### Reads

Implement `IReadOnlyRepository<TEntity, TKey>`:

- Translate `Expression<Func<TEntity, bool>>` conditions and selectors for your store. Throw `NotSupportedException` for expressions you cannot translate; never fall back to filtering on the client.
- Translate `SortOrder<TEntity>.Keys` into your ordering.
- When paging, call `Paging.EnsureValid`, skip `Paging.Offset` items, fall back to `Paging.DefaultOrder<TEntity, TKey>()` when no sort order is given, and build the result with `Paging.ToPage`.
- Exclude soft-deleted entities unless `ReadOptions.IncludeDeleted` is set. Use only the `ReadOptions` members of options meant for another provider.

---

## Unit of work

Derive from `UnitOfWorkBase<TUserKey>` and implement:

| Member | Responsibility |
|---|---|
| `CreateRepository<TEntity, TKey>()` | Your generic repository, bound to this unit of work. Generated code reaches it through `GetRepository` |
| `CommitCore` / `CommitCoreAsync` | Map each staged change to an `EntityChangeKind`, call `Auditor.Stamp(entity, kind, userId, utcNow)` with one `Auditor.UtcNow` for the whole commit, then persist everything, ideally in one transaction |

Report a soft delete as `SoftDeleted` only on the transition from not deleted to deleted, so an edit to an already deleted entity keeps its deletion stamp.

---

## Dependency injection

Implement `IRapidRepoProvider` from `RapidRepo.Extensions.DependencyInjection`, and expose it through an extension on `RapidRepoOptions` that calls `UseProvider`:

```csharp
public static RapidRepoOptions UseDapper(this RapidRepoOptions options, Action<DapperOptions> configure)
    => options.UseProvider(new DapperProvider(configure));
```

| Member | Responsibility |
|---|---|
| `Name` | Shown in diagnostics |
| `AdditionalRootInterfaces` | Your own open generic repository interfaces, for example `IDapperRepository<,>`. Scanning registers repositories against interfaces derived from them, never against them |
| `Register(services, context)` | Your registrations: the generic repository when `context.RegisterGenericRepositories` is set, and your data source. `context` lists the scanned repositories and units of work |

See [Dependency Injection — providers](../RapidRepo.Extensions.DependencyInjection/dependency-injection.md#providers).

---

## Source generator

`[GenerateUnitOfWork]` accepts entity types directly, so a provider without a `DbContext` gets the same generated properties:

```csharp
[GenerateUnitOfWork(typeof(Product), typeof(Order))]
public partial class AppUnitOfWork : DapperUnitOfWork<Guid>, IAppUnitOfWork { ... }
```

The generated code calls only `GetRepository` and `ResolveRepository` on `UnitOfWorkBase<TUserKey>`.

---

## Conformance suite

`RapidRepo.Testing.Conformance` (in this repository; not published) holds provider-neutral xUnit tests for the contract: CRUD, staged changes, soft delete and restore, audit stamping, and querying. To run them against a provider:

1. Implement `IConformanceStore`: a parameterless constructor that creates an empty store holding the conformance entities `Widget` and `Note`, with soft-deleted `Note`s excluded from reads; and `OpenSession(TimeProvider clock)`.
2. Implement `IConformanceSession`: a unit of work whose default user is `ConformanceDefaults.DefaultUser` and which stamps with `clock`, plus `Repository<TEntity, TKey>()` returning repositories that stage changes in it.
3. Add one class per test area to the provider's test project:

```csharp
public sealed class DapperCrudConformanceTests : CrudConformanceTests<DapperConformanceStore>;
public sealed class DapperStagedChangesConformanceTests : StagedChangesConformanceTests<DapperConformanceStore>;
public sealed class DapperSoftDeleteConformanceTests : SoftDeleteConformanceTests<DapperConformanceStore>;
public sealed class DapperAuditConformanceTests : AuditConformanceTests<DapperConformanceStore>;
public sealed class DapperQueryConformanceTests : QueryConformanceTests<DapperConformanceStore>;
```

The suite reads persisted state through a fresh session, so a provider's own caching or change tracking cannot hide a difference. The EF provider runs it against EF Core InMemory and SQLite; see `RapidRepo.EntityFrameworkCore.Tests/Conformance`.
