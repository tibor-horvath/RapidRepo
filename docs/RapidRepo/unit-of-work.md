# Unit of Work

The Unit of Work groups one or more repositories under a single `DbContext`, coordinates commits, and automatically applies audit fields.

---

## Define the interface

```csharp
public interface IAppUnitOfWork : IUnitOfWork<Guid>, IDisposable
{
    IProductRepository Products { get; }
}
```

---

## Implement the class

Inherit from `UnitOfWork<TUserKey>` and pass the default user key to the base constructor. This value is used for audit fields when no `userId` is passed to `Commit`/`CommitAsync`.

```csharp
public class AppUnitOfWork : UnitOfWork<Guid>, IAppUnitOfWork
{
    public IProductRepository Products { get; }

    public AppUnitOfWork(
        AppDbContext dbContext,
        IProductRepository productRepository)
        : base(dbContext, Guid.Empty)
    {
        Products = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
    }
}
```

---

## Repository factory (GetRepository)

Instead of injecting each repository through the constructor, you can use the built-in
`GetRepository<TEntity, TKey>()` factory method. This is especially useful when combined with
application-level entity base classes, because the user-key type is already encoded in the
entity and in the UoW — no extra type parameters are needed anywhere else.

The pattern works with any value type for the user ID (`Guid`, `long`, `int`, etc.):

```csharp
// Guid user keys
public class GuidUnitOfWork : UnitOfWork<Guid>
{
    public GuidUnitOfWork(AppDbContext dbContext) : base(dbContext, Guid.Empty) { }

    public IRepository<Employee, int>  Employees => GetRepository<Employee, int>();
    public IRepository<Product, long>  Products  => GetRepository<Product, long>();
}

// long user keys
public class LongUnitOfWork : UnitOfWork<long>
{
    public LongUnitOfWork(AppDbContext dbContext) : base(dbContext, 0L) { }

    public IRepository<Employee, int>  Employees => GetRepository<Employee, int>();
    public IRepository<Product, long>  Products  => GetRepository<Product, long>();
}
```

> The existing constructor-injection pattern continues to work — `GetRepository` is an opt-in
> convenience.

---

## Source generator (zero-boilerplate)

`RapidRepo.SourceGenerators` eliminates all repository property boilerplate. Add
`[GenerateUnitOfWork(typeof(AppDbContext))]` to a `partial` class and the generator emits the
properties and their interface automatically.

See [RapidRepo.SourceGenerators — Unit of Work](../RapidRepo.SourceGenerators/unit-of-work.md).

---

## DI registration

Register the `DbContext`, repositories, and Unit of Work with a scoped lifetime (matching `DbContext`):

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IAppUnitOfWork, AppUnitOfWork>();
```

For automatic repository discovery via `AddRapidRepo`, see [Dependency Injection](../RapidRepo.Extensions.DependencyInjection/dependency-injection.md).

When using the [source generator](../RapidRepo.SourceGenerators/unit-of-work.md), pass `IServiceProvider` to the base constructor so `ResolveRepository<>()` can resolve custom repos.

---

## Committing changes

Stage changes via repository methods, then persist them in a single call:

```csharp
// Async (preferred)
await _unitOfWork.CommitAsync();

// Sync
_unitOfWork.Commit();
```

### Passing a user ID for auditing

Pass the current user's ID to override the default user key for `CreatedBy`/`ModifiedBy`:

```csharp
await _unitOfWork.CommitAsync(userId: currentUserId);
```

---

## How auditing works

When `Commit`/`CommitAsync` is called, the Unit of Work inspects the EF change tracker and:

| Entity state | Action |
|---|---|
| `Added` + `IAuditableEntity` | Sets `CreatedAt = UtcNow` |
| `Added` + `IAuditableEntity<TUserKey>` | Sets `CreatedAt = UtcNow`, `CreatedBy = userId ?? defaultUserKey` |
| `Modified` + `IAuditableEntity` | Sets `ModifiedAt = UtcNow` |
| `Modified` + `IAuditableEntity<TUserKey>` | Sets `ModifiedAt = UtcNow`, `ModifiedBy = userId ?? defaultUserKey` |
| `IDeletableEntity` with `DeletedAt != null` | Sets `DeletedAt = UtcNow` |
| `IDeletableEntity<TUserKey>` with `DeletedAt != null` | Sets `DeletedAt = UtcNow`, `DeletedBy = userId ?? defaultUserKey` |

No manual timestamp management is needed in your application code.
