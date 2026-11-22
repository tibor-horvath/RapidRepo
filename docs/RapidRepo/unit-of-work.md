# Unit of Work

The Unit of Work groups repositories that share one set of staged changes, commits them together, and stamps [audit fields](auditing.md). The contract, `IUnitOfWork<TUserKey>`, and the audit rules are provider-neutral; a provider supplies the implementation, for example `EfUnitOfWork<TUserKey>` in [RapidRepo.EntityFrameworkCore](../RapidRepo.EntityFrameworkCore/entity-framework-core.md#unit-of-work).

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

`RapidRepo.SourceGenerators` writes those properties for you. Add `[GenerateUnitOfWork(typeof(AppDbContext))]`, or pass entity types, to a `partial` class. See [RapidRepo.SourceGenerators — Unit of Work](../RapidRepo.SourceGenerators/generate-unit-of-work.md).

---

## Committing changes

Stage changes through the repositories, then persist them in a single call:

```csharp
await _unitOfWork.CommitAsync();   // async (preferred)
_unitOfWork.Commit();              // sync
```

At commit the unit of work:

- stamps the audit fields, with the user passed to `Commit`, else the one from an `ICurrentUserProvider<TUserKey>`, else its default user key. See [Auditing](auditing.md), including [the current user](auditing.md#the-current-user) and [controlling the clock](auditing.md#controlling-the-clock);
- checks and increments `Version` on [versioned entities](concurrency.md), and throws `ConcurrencyException` on a conflict;
- persists every staged change.

```csharp
await _unitOfWork.CommitAsync(userId: currentUserId);
```

---

## Disposal

A unit of work does not own the provider's resources it was given. `EfUnitOfWork` never disposes its `DbContext`; the DI container, or whoever created the context, does.
