# Writing a provider

RapidRepo's core package defines the contracts and the rules; a provider implements them for one persistence technology. `RapidRepo.EntityFrameworkCore` is the reference provider. This page lists what a new one has to supply and what it gets from the core for free. The examples call the new provider `MyOrm`.

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
| `EntityChangeClassifier` | `RapidRepo.Auditing` | Tells a soft delete or a restore from a plain update, from the original `DeletedAt` |
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

- Translate `Expression<Func<TEntity, bool>>` conditions for your store. Throw `NotSupportedException` for expressions you cannot translate; never fall back to filtering on the client. Every provider must support at least the conditions below; `QueryExpressionConformanceTests` checks them.
- Run selectors in the store when you can. A provider that cannot translate them may load the matching rows and apply the compiled selector in memory: the result is the same, only more columns are read. Filtering, sorting and paging must still happen in the store.
- Translate `SortOrder<TEntity>.Keys` into your ordering. Each key is a direct property access; throw `NotSupportedException` for anything else you cannot translate.
- When paging, call `Paging.EnsureValid`, skip `Paging.Offset` items, fall back to `Paging.DefaultOrder<TEntity, TKey>()` when no sort order is given, and build the result with `Paging.ToPage`.
- Exclude soft-deleted entities unless `ReadOptions.IncludeDeleted` is set. Use only the `ReadOptions` members of options meant for another provider.

#### Conditions every provider must translate

| Expression | Example |
|---|---|
| Comparisons `==`, `!=`, `<`, `<=`, `>`, `>=`, with the property on either side | `p => p.Price >= 10`, `p => 10 <= p.Price` |
| `&&`, `\|\|` and `!` | `p => p.InStock && !(p.Price > 100)` |
| Captured variables, members of captured objects and method calls on them, evaluated once before translation | `p => p.Name == name`, `p => p.Price > filter.MinPrice` |
| `== null` and `!= null` on nullable properties, including a captured value that is `null` | `p => p.DeletedAt != null`, `p => p.ModifiedBy == userId` |
| `string.StartsWith`, `EndsWith` and `Contains` with a value argument, which match literally (`%` and `_` are not wildcards) | `p => p.Name.StartsWith("Pro")` |
| `Contains` on an in-memory collection of values | `p => ids.Contains(p.Id)`, `p => names.Contains(p.Name)` |
| Comparisons of `DateTime` properties, in chronological order | `p => p.CreatedAt >= since` |

Watch for `Contains` on an array: since C# 14 it binds to `MemoryExtensions.Contains(ReadOnlySpan<T>, T)` through an implicit array-to-span conversion, so a hand-written translator has to unwrap that conversion and evaluate the array instead.

---

## Unit of work

Derive from `UnitOfWorkBase<TUserKey>` and implement:

| Member | Responsibility |
|---|---|
| `CreateRepository<TEntity, TKey>()` | Your generic repository, bound to this unit of work. Generated code reaches it through `GetRepository` |
| `CommitCore` / `CommitCoreAsync` | Map each staged change to an `EntityChangeKind` (inserts are `Added`; for updates call `EntityChangeClassifier.ClassifyUpdate(entity, originalDeletedAt)`), call `Auditor.Stamp(entity, kind, userId, utcNow)` with one `Auditor.UtcNow` for the whole commit, then persist everything, ideally in one transaction. `userId` arrives already resolved from the commit's argument and the current-user provider, so a provider needs no code for either |

Offer a constructor that takes an `ICurrentUserProvider<TUserKey>?` and passes it to the `UnitOfWorkBase` constructor, as `EfUnitOfWork` does, so applications that don't pass an `IServiceProvider` can still supply one.

### Optimistic concurrency

For each staged change of an `IVersionedEntity`, a provider must:

1. **Update** (`Modified`, `SoftDeleted`, `Restored`): call `VersionStamper.Stamp(entity)`, which increments `Version` and returns the expected version, and write the row only `WHERE Version = expected`.
2. **Hard delete:** delete only `WHERE Version = entity.Version`.
3. **Insert:** write `Version` as it is.
4. Treat zero affected rows as a conflict and throw `new ConcurrencyException(entities, yourException)`. That constructor writes the message from the entity types, so every provider reports a conflict the same way.
5. Persist nothing from a commit that conflicts, so run the commit in one transaction.
6. If the commit fails for any reason, call `VersionStamper.Revert(entity, expected)` for every stamped entity.

These predicates identify a row by key and version, so they cannot tell a row from a later one that reuses its key and has reached the same version. RapidRepo leaves key reuse for versioned entities unsupported and documents it as such ([Optimistic Concurrency](concurrency.md#how-the-version-changes)); a provider for a store that reuses keys must add its own row discriminator to the predicates.

The entity's current `Version` is the expected one, whether the entity was loaded or built from a request, so a provider needs no record of the version it read. The `ConcurrencyConformanceTests` check all of this, and `ConcurrencyAtomicityConformanceTests` check step 5.

### Transactions

Transactions are optional: without an override, the transaction members throw `NotSupportedException`, so a provider compiles and passes the rest of the suite without them. To support them:

| Member | Responsibility |
|---|---|
| `BeginTransactionCoreAsync(isolationLevel, cancellationToken)` | Required. Open a transaction and return it as an `IUnitOfWorkTransaction` that rolls back when disposed without `CommitAsync`. When a transaction is already open, return `JoinedTransaction.Instance` instead, and throw `InvalidOperationException` if `isolationLevel` is stricter than the open transaction's. Pass `isolationLevel` through, and leave the database's default when it is `null` |
| `ExecuteInTransactionCoreAsync(operation, isolationLevel, cancellationToken)` | Optional. The default opens a transaction with `BeginTransactionCoreAsync` and runs `operation` through `RunInTransactionAsync`, which commits when it completes. Override it to add retries, as `EfUnitOfWork` does with the execution strategy; reuse `RunInTransactionAsync` inside |

While a transaction is open, `CommitCore` / `CommitCoreAsync` must write in it rather than open their own. The `TransactionConformanceTests` check commit, rollback, joining and the hand-opened form.

Report a soft delete as `SoftDeleted` only on the transition from not deleted to deleted, so an edit to an already deleted entity keeps its deletion stamp. `EntityChangeClassifier` applies this rule; it needs the `DeletedAt` the entity had before the change. A change-tracking provider reads its original value. A command-based provider records it the first time it stages the entity, before `StageDeletionChange` calls `applyChange`.

---

## Dependency injection

Implement `IRapidRepoProvider` from `RapidRepo.Extensions.DependencyInjection`, and expose it through an extension on `RapidRepoOptions` that calls `UseProvider`:

```csharp
public static RapidRepoOptions UseMyOrm(this RapidRepoOptions options, Action<MyOrmOptions> configure)
    => options.UseProvider(new MyOrmProvider(configure));
```

| Member | Responsibility |
|---|---|
| `Name` | Shown in diagnostics |
| `AdditionalRootInterfaces` | Your own open generic repository interfaces, for example `IMyOrmRepository<,>`. Scanning registers repositories against interfaces derived from them, never against them |
| `Register(services, context)` | Your registrations: the generic repository when `context.RegisterGenericRepositories` is set, against `context.RootInterfaces` (the core interfaces followed by yours), and your data source. `context` lists the scanned repositories and units of work |

See [Dependency Injection — providers](../RapidRepo.Extensions.DependencyInjection/dependency-injection.md#providers).

---

## Source generator

`[GenerateUnitOfWork]` accepts entity types directly, so a provider without a `DbContext` gets the same generated properties:

```csharp
[GenerateUnitOfWork(typeof(Product), typeof(Order))]
public partial class AppUnitOfWork : MyOrmUnitOfWork<Guid>, IAppUnitOfWork { ... }
```

The generated code calls only `GetRepository` and `ResolveRepository` on `UnitOfWorkBase<TUserKey>`.

---

## Conformance suite

`RapidRepo.Testing.Conformance` (in this repository; not published) holds provider-neutral xUnit tests for the contract: CRUD, staged changes, soft delete and restore, audit stamping, optimistic concurrency, transactions, querying, and the condition expressions every provider must translate. To run them against a provider:

1. Implement `IConformanceStore`: a parameterless constructor that creates an empty store holding the conformance entities `Widget`, `Note` and `Ticket`, with soft-deleted `Note`s excluded from reads and `Ticket.Version` checked on update and delete; and `OpenSession(TimeProvider clock, ICurrentUserProvider<Guid>? currentUserProvider = null)`.
2. Implement `IConformanceSession`: a unit of work whose default user is `ConformanceDefaults.DefaultUser` and which stamps with `clock`, plus `Repository<TEntity, TKey>()` returning repositories that stage changes in it. Pass `currentUserProvider` to the `UnitOfWorkBase` constructor, which resolves the audit user. The audit tests check that order: explicit user, then the provider, then the default user.

```csharp
public IConformanceSession OpenSession(TimeProvider clock, ICurrentUserProvider<Guid>? currentUserProvider = null)
{
    var unitOfWork = new MyOrmUnitOfWork(
        connection, ConformanceDefaults.DefaultUser, serviceProvider: null, clock, currentUserProvider);
    return new MyOrmConformanceSession(unitOfWork);
}
```
3. Add one class per test area to the provider's test project:

```csharp
public sealed class MyOrmCrudConformanceTests : CrudConformanceTests<MyOrmConformanceStore>;
public sealed class MyOrmStagedChangesConformanceTests : StagedChangesConformanceTests<MyOrmConformanceStore>;
public sealed class MyOrmSoftDeleteConformanceTests : SoftDeleteConformanceTests<MyOrmConformanceStore>;
public sealed class MyOrmAuditConformanceTests : AuditConformanceTests<MyOrmConformanceStore>;
public sealed class MyOrmQueryConformanceTests : QueryConformanceTests<MyOrmConformanceStore>;
public sealed class MyOrmQueryExpressionConformanceTests : QueryExpressionConformanceTests<MyOrmConformanceStore>;
public sealed class MyOrmConcurrencyConformanceTests : ConcurrencyConformanceTests<MyOrmConformanceStore>;
public sealed class MyOrmConcurrencyAtomicityConformanceTests : ConcurrencyAtomicityConformanceTests<MyOrmConformanceStore>;
public sealed class MyOrmTransactionConformanceTests : TransactionConformanceTests<MyOrmConformanceStore>;   // if it supports transactions
```

The suite reads persisted state through a fresh session, so a provider's own caching or change tracking cannot hide a difference. The EF provider runs it against EF Core InMemory and SQLite; see `RapidRepo.EntityFrameworkCore.Tests/Conformance`.
