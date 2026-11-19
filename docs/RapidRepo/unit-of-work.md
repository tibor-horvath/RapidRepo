# Unit of Work

The Unit of Work groups repositories that share one set of staged changes, commits them together, and stamps audit fields. The contract, `IUnitOfWork<TUserKey>`, and the audit rules are provider-neutral; a provider supplies the implementation, for example `EfUnitOfWork<TUserKey>` in [RapidRepo.EntityFrameworkCore](../RapidRepo.EntityFrameworkCore/entity-framework-core.md#unit-of-work).

---

## Define the interface

```csharp
public interface IAppUnitOfWork : IUnitOfWork<Guid>
{
    IProductRepository Products { get; }
}
```

`TUserKey` is the type of the user recorded in audit fields, and must be a value type (`Guid`, `long`, `int`, …).

---

## Implement the class

Derive from your provider's unit of work and pass the default user key, which is recorded when a commit names no user. With EF Core:

```csharp
public class AppUnitOfWork : EfUnitOfWork<Guid>, IAppUnitOfWork
{
    public IProductRepository Products { get; }

    public AppUnitOfWork(AppDbContext dbContext, IProductRepository productRepository)
        : base(dbContext, Guid.Empty)
    {
        Products = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
    }
}
```

The repositories must share the unit of work's changes. With EF Core, that means they use the same `DbContext` instance, which is what a scoped DI registration gives you.

---

## Repository factory (GetRepository)

Instead of injecting each repository through the constructor, use the `GetRepository<TEntity, TKey>()` factory method that every unit of work inherits from `UnitOfWorkBase<TUserKey>`. It returns the provider's generic repository, bound to the unit of work:

```csharp
public class AppUnitOfWork : EfUnitOfWork<Guid>
{
    public AppUnitOfWork(AppDbContext dbContext) : base(dbContext, Guid.Empty) { }

    public IRepository<Employee, int> Employees => GetRepository<Employee, int>();
    public IRepository<Product, long> Products  => GetRepository<Product, long>();
}
```

`ResolveRepository<TRepo>()` does the same for custom repository interfaces, resolving them from the `IServiceProvider` passed to the constructor.

---

## Source generator (zero-boilerplate)

`RapidRepo.SourceGenerators` writes those properties for you. Add `[GenerateUnitOfWork(typeof(AppDbContext))]`, or pass entity types, to a `partial` class. See [RapidRepo.SourceGenerators — Unit of Work](../RapidRepo.SourceGenerators/unit-of-work.md).

---

## Committing changes

Stage changes through the repositories, then persist them in a single call:

```csharp
await _unitOfWork.CommitAsync();   // async (preferred)
_unitOfWork.Commit();              // sync
```

Pass the current user to record them in the audit fields instead of the default user:

```csharp
await _unitOfWork.CommitAsync(userId: currentUserId);
```

To avoid passing the user to every commit, let the unit of work look it up. See [The current user](#the-current-user).

---

## The current user

An `ICurrentUserProvider<TUserKey>` (namespace `RapidRepo.Auditing`) tells the unit of work who is making the changes, so services can call `CommitAsync()` without threading a user through every method:

```csharp
public interface ICurrentUserProvider<TUserKey> where TUserKey : struct
{
    TUserKey? GetCurrentUserId();   // null when there is none: a background job, an anonymous request
}
```

At commit the audit user is resolved in this order:

```
explicit userId argument  >  ICurrentUserProvider  >  the unit of work's default user key
```

- An explicit `userId` always wins, and the provider is then not asked.
- The provider is asked at most once per commit, so every entity in a commit gets the same user, just as they share one timestamp.
- When the provider returns `null`, the default user key is stamped, for example a system user for jobs and seeding.

### Giving the unit of work a provider

A unit of work finds the provider in one of two ways:

1. **From the `IServiceProvider`** passed to its constructor. A unit of work that already takes one, as the [source generator](../RapidRepo.SourceGenerators/unit-of-work.md) examples do, needs no change:

   ```csharp
   public partial class AppUnitOfWork(AppDbContext db, IServiceProvider sp)
       : EfUnitOfWork<Guid>(db, Guid.Empty, sp), IAppUnitOfWork;
   ```

2. **As a constructor parameter**, for a unit of work that does not take a service provider:

   ```csharp
   public class AppUnitOfWork(AppDbContext db, ICurrentUserProvider<Guid> currentUser)
       : EfUnitOfWork<Guid>(db, Guid.Empty, serviceProvider: null, timeProvider: null, currentUser), IAppUnitOfWork;
   ```

A provider passed to the constructor wins over one in the service provider. The service provider is asked at the first commit, not when the unit of work is created. Register the provider with `UseCurrentUserProvider` or `UseCurrentUser` in `AddRapidRepo`; see [Dependency Injection — Current user](../RapidRepo.Extensions.DependencyInjection/dependency-injection.md#current-user).

### ASP.NET Core example

RapidRepo has no ASP.NET Core dependency, because claim types and user-ID formats vary by application. A provider over `IHttpContextAccessor` takes a few lines:

```csharp
public sealed class HttpContextUserProvider(IHttpContextAccessor accessor) : ICurrentUserProvider<Guid>
{
    public Guid? GetCurrentUserId() =>
        Guid.TryParse(accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : null;
}

// Program.cs
builder.Services.AddHttpContextAccessor();
builder.Services.AddRapidRepo(options =>
{
    options.UseEntityFrameworkCore<AppDbContext>();
    options.UseUnitOfWork<AppUnitOfWork>();
    options.UseCurrentUserProvider<HttpContextUserProvider, Guid>();
});
```

Services then commit without a user:

```csharp
public async Task CreateAsync(Product product)
{
    _unitOfWork.Products.Add(product);
    await _unitOfWork.CommitAsync();   // CreatedBy = the signed-in user
}
```

### Requiring a user

By default a commit with no user stamps the default user key, often `Guid.Empty`, so a forgotten user goes unnoticed. Turn on strict mode to throw instead:

```csharp
options.RequireCurrentUser = true;   // in AddRapidRepo
```

With strict mode on, a commit with neither a `userId` nor a user from the provider throws `InvalidOperationException` as soon as it would stamp a user field, before anything is persisted. The user fields are `CreatedBy` and `ModifiedBy` on `IAuditableEntity<TUserKey>`, and `DeletedBy` on `IDeletableEntity<TUserKey>`. A commit that stamps none of them still succeeds without a user. That includes a commit that changes only entities without user fields, or only `CreatedAt`/`ModifiedAt` on an `IAuditableEntity`. Jobs that legitimately write user fields without a signed-in user pass one explicitly, such as their system user. Strict mode is read from `CurrentUserOptions` in the unit of work's service provider. A unit of work without a service provider can override `RequireCurrentUser` instead:

```csharp
protected override bool RequireCurrentUser => true;
```

---

## How auditing works

At commit, the unit of work works out what happened to each entity and stamps the audit fields, using one timestamp for the whole commit:

| Change | Fields set |
|---|---|
| Added | `CreatedAt`; `CreatedBy` for `IAuditableEntity<TUserKey>` |
| Added and soft-deleted before the commit | As Added, plus `DeletedAt`; `DeletedBy` for `IDeletableEntity<TUserKey>`. The row is inserted as deleted |
| Modified | `ModifiedAt`; `ModifiedBy` for `IAuditableEntity<TUserKey>` |
| Soft-deleted (not deleted → deleted) | `ModifiedAt` / `ModifiedBy`, plus `DeletedAt`; `DeletedBy` for `IDeletableEntity<TUserKey>` |
| Restored (deleted → not deleted) | `ModifiedAt` / `ModifiedBy`; `DeletedAt` and `DeletedBy` cleared |

- The user is the one passed to `Commit`, else the one from the [current-user provider](#the-current-user), else the unit of work's default user.
- User fields are stamped only on entities whose user key type matches the unit of work's `TUserKey`.
- Only the soft-delete *transition* stamps `DeletedAt` / `DeletedBy`. Editing an entity that is already deleted keeps who deleted it and when.

These rules live in the core as `AuditStamper<TUserKey>`, so every provider applies them the same way.

### Controlling the clock

Audit times come from a `TimeProvider`, `TimeProvider.System` by default. Pass your own to make audit times deterministic, for example in tests:

```csharp
public class AppUnitOfWork(AppDbContext db, TimeProvider clock)
    : EfUnitOfWork<Guid>(db, Guid.Empty, serviceProvider: null, timeProvider: clock), IAppUnitOfWork;
```

---

## Disposal

A unit of work does not own the provider's resources it was given. `EfUnitOfWork` never disposes its `DbContext`; the DI container, or whoever created the context, does.
