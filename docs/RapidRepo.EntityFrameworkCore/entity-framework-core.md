# EF Core

`RapidRepo.EntityFrameworkCore` implements the RapidRepo contracts on top of an EF Core `DbContext`. It targets .NET 10 (EF Core 10) and .NET 11 (EF Core 11).

```bash
dotnet add package RapidRepo.EntityFrameworkCore
```

The package depends on `RapidRepo` (entities and contracts) and `RapidRepo.Extensions.DependencyInjection` (`AddRapidRepo`), so it is the only package an EF Core application needs, plus `RapidRepo.SourceGenerators` if you want generated unit of work properties.

---

## Setting up the `DbContext`

Map your entities as usual, and let RapidRepo add the soft-delete filter to every entity that implements `IDeletableEntity`. In an inheritance hierarchy the filter goes on the root type and covers the types below it (see [Inheritance](#inheritance)):

```csharp
using RapidRepo.Extensions;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.AddConcurrencyTokens();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ... entity configuration ...

        modelBuilder.AddSoftDeleteQueryFilters();
    }
}
```

Call `AddSoftDeleteQueryFilters()` after the entity types are configured. `AddConcurrencyTokens()` is a convention, so it applies to every entity type wherever it is configured. See [Soft delete and query filters](#soft-delete-and-query-filters) and [Optimistic concurrency](#optimistic-concurrency).

---

## Repositories

| Type | Purpose |
|---|---|
| `EfRepository<TEntity, TId>` | Full read and write repository. Use it directly, or derive from it for a custom repository |
| `EfRepository<TEntity, TId, TContext>` | The same, bound to a specific context type; registered by `UseEntityFrameworkCore<TContext>()` |
| `EfReadOnlyRepository<TEntity, TId>` | Read-only base class |
| `EfWriteRepository<TEntity, TId>` | Write-only base class |
| `IEfRepository<TEntity, TId>` / `IEfReadOnlyRepository<TEntity, TId>` | The contracts plus `Query()` |

```csharp
using RapidRepo.EntityFrameworkCore;

public interface IProductRepository : IRepository<Product, long>
{
    Task<IEnumerable<Product>> GetByCategoryAsync(long categoryId);
}

public class ProductRepository(AppDbContext context) : EfRepository<Product, long>(context), IProductRepository
{
    public Task<IEnumerable<Product>> GetByCategoryAsync(long categoryId)
        => GetAllAsync(p => p.CategoryId == categoryId, SortOrder<Product>.By(p => p.Name));
}
```

The write methods of `EfRepository` are virtual, and the write rules call them through the repository. An override of `HardDelete`, for example, also sees the hard deletes that `Delete`, `DeleteById` and `HardDeleteById` make. To change how changes are staged in the `DbContext` rather than what an operation does, derive from `EfWriteRepository<TEntity, TId>` and override its `Stage*` hooks.

### Which context to take

The base classes accept any `DbContext`. Taking your derived context (`AppDbContext`) resolves with no further setup, because `AddDbContext<AppDbContext>()` registers `AppDbContext` and not the base `DbContext` type. Taking the base `DbContext` keeps a repository reusable across contexts, but needs a forwarder, which [`UseEntityFrameworkCore<TContext>()`](ef-core-registration.md) registers for you. In an application with several contexts, always take the derived context.

---

## Unit of work

```csharp
public interface IAppUnitOfWork : IUnitOfWork<Guid>
{
    IProductRepository Products { get; }
}

public class AppUnitOfWork(AppDbContext db, IProductRepository products, IServiceProvider services)
    : EfUnitOfWork<Guid>(db, Guid.Empty, services), IAppUnitOfWork
{
    public IProductRepository Products { get; } = products;
}
```

| Constructor | Use when |
|---|---|
| `(DbContext, TUserKey defaultUserKey)` | Repositories are injected or come from `GetRepository`, and no current-user provider or `RequireCurrentUser` is configured: without a service provider, the unit of work cannot see them |
| `(DbContext, TUserKey, IServiceProvider)` | The usual choice with DI: finds the current-user provider and `RequireCurrentUser`, and lets `ResolveRepository<TRepo>()` resolve custom repositories |
| `(DbContext, TUserKey, IServiceProvider?, TimeProvider?)` | You also want to control the audit clock |
| `(DbContext, TUserKey, IServiceProvider?, TimeProvider?, ICurrentUserProvider<TUserKey>?)` | You pass the [current-user provider](../RapidRepo/auditing.md#the-current-user) directly instead of through the service provider |

At commit, `EfUnitOfWork` reads each change from the change tracker, stamps the audit fields, and calls `SaveChanges`. A soft delete or restore is recognised by comparing the original and current `DeletedAt`, so only the transition stamps `DeletedAt` / `DeletedBy`. See [How auditing works](../RapidRepo/auditing.md#how-auditing-works).

`EfUnitOfWork` never disposes the `DbContext`; whoever created it does.

### Transactions

`EfUnitOfWork` implements [transactions](../RapidRepo/transactions.md) over `DbContext.Database`:

- `ExecuteInTransactionAsync` runs the whole transaction through the context's execution strategy (`Database.CreateExecutionStrategy()`). With `EnableRetryOnFailure`, or any strategy that retries on failure, a transient failure retries the transaction as a whole, instead of failing with *"The configured execution strategy ... does not support user-initiated transactions"*. Before a retry, the change tracker is cleared, so the entities the failed attempt tracked are not saved twice.
- If the connection fails while the transaction itself is committing, the database may have committed it even though an error was reported, and the retry then runs the whole transaction again. Make the work safe to repeat where that matters, for example with a unique key that rejects the second insert, or check for it at the start of the delegate. See EF Core's [Transaction commit failure and the idempotency issue](https://learn.microsoft.com/ef/core/miscellaneous/connection-resiliency#transaction-commit-failure-and-the-idempotency-issue).
- `BeginTransactionAsync` can't be retried by the strategy, so with a strategy that retries on failure it throws `InvalidOperationException` pointing to `ExecuteInTransactionAsync`. Without one, it opens a transaction with `Database.BeginTransactionAsync`.
- Both join a transaction that is already open on the context (`Database.CurrentTransaction`), including one you opened yourself. A requested `isolationLevel` stricter than the open transaction's throws `InvalidOperationException`; the check is skipped when the open transaction's level can't be read.
- Both open their transaction through `BeginTransactionCoreAsync`, so an override of it in your derived unit of work applies to either.
- An `isolationLevel` is passed to `Database.BeginTransactionAsync(isolationLevel)`, which needs a relational provider.
- EF Core InMemory doesn't support transactions and, by default, throws on `BeginTransaction` (`TransactionIgnoredWarning`).

---

## Read options

Every read takes a `ReadOptions`. Pass an `EfReadOptions<TEntity>` for the EF Core specific hints:

| Property | Default | Description |
|---|---|---|
| `IncludeDeleted` | `false` | Also return soft-deleted rows (inherited from `ReadOptions`) |
| `Include` | `null` | Related data to load, e.g. `q => q.Include(p => p.Category)` |
| `Track` | `true` | Whether returned entities are tracked. Projections, counts and existence checks are never tracked |
| `SplitQuery` | `false` | Load collection includes through [split queries](https://learn.microsoft.com/ef/core/querying/single-split-queries) |
| `OrderBy` | `null` | An ordering applied to the query directly. Overrides the `SortOrder` argument; use it only for orderings a `SortOrder` cannot express |
| `IgnoreAllQueryFilters` | `false` | Bypass every global query filter, not only soft delete |

```csharp
var products = await repo.GetAllAsync(
    condition: p => p.IsActive,
    orderBy: SortOrder<Product>.By(p => p.Name),
    options: new EfReadOptions<Product>
    {
        Include = q => q.Include(p => p.Category).ThenInclude(c => c.Parent),
        Track = false,
    });
```

`EfReadOptions<TEntity>` is meant for repositories of `TEntity`. Passed to a repository of another entity type, or to another provider, only its `IncludeDeleted` applies.

---

## Custom queries

Implement `IEfRepository<TEntity, TKey>` (or `IEfReadOnlyRepository<TEntity, TKey>`) to expose `Query(options)`, an `IQueryable<TEntity>` with the read options applied, for free-form LINQ:

```csharp
public interface IProductRepository : IEfRepository<Product, long>
{
    Task<List<ProductSummary>> SearchAsync(string term, int limit);
}

public class ProductRepository(AppDbContext context) : EfRepository<Product, long>(context), IProductRepository
{
    public Task<List<ProductSummary>> SearchAsync(string term, int limit)
        => Query(new EfReadOptions<Product> { Track = false })
            .Where(p => p.Name.Contains(term))
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new ProductSummary(p.Id, p.Name, p.Price))
            .Take(limit)
            .ToListAsync();
}
```

Deriving from `IEfRepository<,>` ties the interface to EF Core. Keep interfaces on the neutral `IRepository<,>` when they do not need it, and use the protected `BuildQuery(condition, orderBy, options)` inside the class instead; it applies the same pipeline and stays an implementation detail.

---

## Soft delete and query filters

`AddSoftDeleteQueryFilters()` adds `e => e.DeletedAt == null` to every root entity type that implements `IDeletableEntity`, as a **named** query filter, `RapidRepo.SoftDelete` (`ModelBuilderExtensions.SoftDeleteFilterKey`). Because it is named, other named filters can sit next to it, and `IncludeDeleted` bypasses it alone:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.AddSoftDeleteQueryFilters();
    modelBuilder.Entity<Invoice>().HasQueryFilter("Tenant", i => i.TenantId == _tenantId);
    // Or let RapidRepo isolate tenants: modelBuilder.AddTenantQueryFilters(this). See "Tenant query filters" below.
}

// Deleted invoices of the current tenant only; the tenant filter stays in force.
var invoices = await repo.GetAllAsync(options: new ReadOptions { IncludeDeleted = true });

// RestoreById and HardDeleteById(includeDeleted: true) cannot reach another tenant's rows either.
repo.HardDeleteById(id, includeDeleted: true);
```

To bypass soft delete in a hand-written query, use EF Core directly: `query.IgnoreQueryFilters([ModelBuilderExtensions.SoftDeleteFilterKey])`.

`IncludeDeleted` never lifts a filter it does not have to. On an entity without the soft-delete filter and without an anonymous filter, for example an entity that is not `IDeletableEntity` but has a named tenant filter, it bypasses nothing.

### Inheritance

EF Core only allows query filters on the root of an inheritance hierarchy, so the soft-delete filter is declared on the root and covers every derived type. The root must therefore implement `IDeletableEntity` whenever a type below it does; otherwise `AddSoftDeleteQueryFilters()` throws, rather than leave soft-deleted rows of that type visible.

### Entities with an anonymous filter

EF Core does not allow anonymous and named filters on the same entity type. An entity that already has an anonymous filter when `AddSoftDeleteQueryFilters()` runs gets an anonymous soft-delete filter instead, which replaces the existing one, as in 8.x. A soft-delete filter you declare by hand without a name, such as `HasQueryFilter(e => e.DeletedAt == null)`, is anonymous too. For these entities RapidRepo cannot tell the soft-delete filter apart from the rest, so `IncludeDeleted` bypasses every filter on them.

Combine soft delete with tenant isolation through named filters, as above, rather than in one anonymous filter.

---

## Tenant query filters

For the provider-neutral rules (stamping, write guards, scopes), see [Multi-Tenancy](../RapidRepo/multi-tenancy.md). With EF Core, the context supplies the tenant and registers the filter:

```csharp
public sealed class AppDbContext(DbContextOptions<AppDbContext> options, ICurrentTenantProvider<Guid> tenants)
    : DbContext(options), ITenantDbContext<Guid>
{
    public Guid CurrentTenantId => tenants.GetCurrentTenantId();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ... entity configuration ...
        modelBuilder.AddSoftDeleteQueryFilters();
        modelBuilder.AddTenantQueryFilters(this);
    }
}
```

- `AddTenantQueryFilters(this)` adds a **named** filter, `RapidRepo.Tenant` (`ModelBuilderExtensions.TenantFilterKey`), to the root of every entity hierarchy that implements `ITenantEntity<TTenantKey>`. It sits next to the soft-delete filter, and `IncludeDeleted` never lifts it.
- The filter reads the tenant **on every query**, through the context: the innermost `ITenantScope.Override`, else `CurrentTenantId`. Inside a host scope it matches every row. Without a tenant it matches no rows, and repository reads throw before the query is built.
- Constructing the context never reads the tenant, so `dotnet ef migrations` needs none.
- `EfUnitOfWork` reads the tenant from the same `CurrentTenantId`, so reads and commits always agree.
- Every tenant entity of a context uses the same tenant key type. Map entities with another key type in a separate context.
- `AddTenantQueryFilters` never replaces an existing anonymous filter, because that would silently drop your own tenant condition. It throws instead; see [Migrating a hand-written tenant filter](../RapidRepo/multi-tenancy.md#migrating-a-hand-written-tenant-filter).
- `EfReadOptions.IgnoreAllQueryFilters` on a tenant entity throws outside a host scope. On a tenant entity with an anonymous filter, `IncludeDeleted` throws rather than lift every filter. Non-tenant entities keep the 8.x fallback described above.
- `ExecuteUpdate` and `ExecuteDelete` honour the filter, so they stay scoped to the current tenant. They bypass stamping and the write guards, so never set `TenantId` in them.

**Context pooling.** With `AddDbContextPool`, a context outlives the scope it was leased in and can't take scoped services. Read the tenant from ambient state instead, for example through a singleton provider over `IHttpContextAccessor`. The filter reads `CurrentTenantId` per query, so a reused instance always sees the current request's tenant.

### Tenancy validation

`TenancyValidator` checks a context's model:

| Check | Why |
|---|---|
| Every tenant entity has the `RapidRepo.Tenant` filter on its hierarchy root. | Otherwise writes would be guarded while reads returned every tenant's rows. |
| No tenant entity has an anonymous filter. | EF Core doesn't allow anonymous and named filters together, and `IncludeDeleted` couldn't lift soft delete alone. |
| The context implements `ITenantDbContext<TTenantKey>`, with the entities' key type. | Neither the filter nor the unit of work could find the tenant. |
| All tenant entities share one key type. | The filter and the stamping would compare different types. |
| A tenant provider is registered (startup only). | The first request would throw. |
| Foreign keys between tenant entities include `TenantId` (ownerships aside). | A warning by default; an error with `RequireTenantScopedForeignKeys`. The commit checks exactly the same foreign keys. |

A failure throws one `RapidRepoConfigurationException` (an `InvalidOperationException`) whose `Problems` lists every problem with its fix, as does its message. The check runs:

- **on first use**, once per model, the first time a repository reads or a unit of work commits a tenant entity. It always runs and needs no host;
- **at startup**, through the hosted service `AddRapidRepo` registers when a tenant provider is configured, so `host.StartAsync()` fails;
- **explicitly**, with `serviceProvider.ValidateRapidRepoTenancy()`, or with `TenancyValidator.Validate(context)` in a test, which also returns the warnings.

A context without tenant entities always passes, without any other work.

---

## Optimistic concurrency

`configurationBuilder.AddConcurrencyTokens()`, called in `ConfigureConventions`, marks the `Version` property of every entity that implements `IVersionedEntity` as a concurrency token (`IsConcurrencyToken()`). EF Core then adds `WHERE Version = @expected` to each update and delete of those entities.

- `Version` is a plain `bigint` column that the unit of work increments. It is not a `rowversion` or PostgreSQL `xmin` column, so it behaves the same on every database. Adding `IVersionedEntity` to an existing entity needs a migration for the new column.
- Explicit configuration wins over the convention, so `Property(e => e.Version).IsConcurrencyToken(false)` opts an entity out. The unit of work then refuses to commit that entity (see below).

- If an `IVersionedEntity`'s `Version` is not a concurrency token, a commit that writes the entity throws `InvalidOperationException` and saves nothing. Forgetting `AddConcurrencyTokens()` can't lose updates silently. Marking the property yourself, for example with `[ConcurrencyCheck]`, also works.
- `EfUnitOfWork` translates EF Core's `DbUpdateConcurrencyException` into `ConcurrencyException` when the conflict involves an `IVersionedEntity`, and keeps EF's exception as `InnerException`. EF Core stops at the first conflicting batch, so `Entities` may not list every stale entity.
- Any other `DbUpdateConcurrencyException` is thrown unchanged, as before. Examples are a concurrency token you configured yourself, or an update of a row that no longer exists for an entity without `IVersionedEntity`.
- A relational database saves nothing from a conflicting commit, because `SaveChanges` runs in a transaction. EF Core InMemory has no transactions, so changes written before the conflict stay written there.
- The version covers the entity's own row. Changing only an owned entity, or only a collection of related entities, doesn't update the owner's row, so it doesn't increment the owner's version.

---

## Change tracking and portability

EF Core tracks loaded entities, so changing one and committing persists the change even without `Update`. Other providers do not, so call `Update` to keep the code portable. Reads track by default; pass `new EfReadOptions<T> { Track = false }` for read-only paths.

`DeleteById`, `RestoreById` and `HardDeleteById` also find an entity that was added but not yet committed, as long as its key is already final. Like `DbSet.Find`, they check the change tracker without running `DetectChanges` first, so their cost doesn't grow with the number of tracked entities. An entity that is reachable only through a navigation of a tracked entity, and that the tracker has not detected yet, is not found. Add it with `Add` to make it findable.

---

## 8.x names

The 8.x class names still compile as obsolete subclasses, reporting warning `RRMIG001`, and will be removed in 10.0:

| 8.x | 9.0 |
|---|---|
| `BaseRepository<,>`, `Repository<,>` | `EfRepository<,>` |
| `Repository<,,>` | `EfRepository<,,>` |
| `ReadOnlyRepository<,>` | `EfReadOnlyRepository<,>` |
| `WriteRepository<,>`, `BaseWriteRepository<,>` | `EfWriteRepository<,>` |
| `UnitOfWork<>` | `EfUnitOfWork<>` |

See the [migration guide](../migration/8.x-to-9.0.md).
