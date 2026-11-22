# EF Core registration

`UseEntityFrameworkCore` plugs the EF Core provider into [`AddRapidRepo`](../RapidRepo.Extensions.DependencyInjection/dependency-injection.md). Scanning, filters, lifetimes and units of work work the same with any provider; this page covers what the EF Core provider adds.

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddRapidRepo(options =>
{
    options.UseEntityFrameworkCore<AppDbContext>();
    options.ScanAssembliesContaining<ProductRepository>();
    options.RegisterGenericRepositories = true;
    options.UseUnitOfWork<AppUnitOfWork>();
});
```

`UseEntityFrameworkCore` is in the `Microsoft.Extensions.DependencyInjection` namespace, like `AddRapidRepo`. Configure one provider per `AddRapidRepo` call.

---

## `UseEntityFrameworkCore<TContext>()`

`AddDbContext<AppDbContext>()` registers `AppDbContext`. It does **not** register the base `DbContext` type, so repositories and units of work that take a `DbContext` constructor parameter cannot be activated by the container:

```
System.InvalidOperationException: Unable to resolve service for type
'Microsoft.EntityFrameworkCore.DbContext' while attempting to activate 'ProductRepository'.
```

Binding the call to the context closes that gap. It does two things:

1. **Registers a `DbContext` → `TContext` forwarder**, but only when something registered by this call actually takes a base `DbContext` parameter. The forwarder hands out the same instance `AddDbContext` created, so repositories and the unit of work share one change tracker.
2. **Binds generic repositories to `TContext`.** With `RegisterGenericRepositories = true`, you get a closed registration per eligible `DbSet<TEntity>` on `TContext`, backed by `EfRepository<TEntity, TId, TContext>`, instead of one open-generic fallback. Each is registered for `IRepository<,>`, `IReadOnlyRepository<,>`, `IWriteRepository<,>`, `IEfRepository<,>` and `IEfReadOnlyRepository<,>`.

An entity is eligible when both hold:

- **It derives from `BaseEntity<TId>`.** A `DbSet<T>` for a plain EF entity is skipped, without an error.
- **It has a `DbSet<>` of its own.** Discovery reflects over `DbSet<>` properties (public or not, matching how EF finds them) rather than building the model, so an entity EF reaches only through a navigation is not seen.

For anything outside those rules, register a repository explicitly.

An application that already registers its own forwarder keeps it:

```csharp
builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<AppDbContext>());
```

With `RegisterAsSelf = true`, each `EfRepository<TEntity, TId, TContext>` is also registered against itself.

---

## `UseEntityFrameworkCore()` without a context

```csharp
builder.Services.AddRapidRepo(options =>
{
    options.UseEntityFrameworkCore();
    options.ScanAssembliesContaining<ProductRepository>();
    options.RegisterGenericRepositories = true;
});
```

Without a context, `RegisterGenericRepositories` registers `EfRepository<,>` as an open-generic fallback, which takes the base `DbContext`. Repositories that take the base `DbContext` then rely on the application to register it.

When something needs the base `DbContext` and nothing registers it, `AddRapidRepo` writes a `Trace` warning. It does not throw by default, because an application may register its `DbContext` *after* calling `AddRapidRepo`, and that cannot be told apart at registration time. To fail fast instead:

```csharp
options.UseEntityFrameworkCore(ef => ef.ThrowOnMissingDbContext = true);
```

Configure the EF Core provider even when you do not need a context binding: it is what tells scanning that `IEfRepository<,>` is a root interface and not one of your repository interfaces.

---

## Multi-DbContext applications

Call `AddRapidRepo` once per bounded context, each naming its own context and scoped to its own assemblies and unit of work:

```csharp
builder.Services.AddDbContext<SalesDbContext>(/* ... */);
builder.Services.AddDbContext<BillingDbContext>(/* ... */);

builder.Services.AddRapidRepo(options =>
{
    options.UseEntityFrameworkCore<SalesDbContext>();
    options.ScanAssembliesContaining<SalesModuleMarker>();
    options.RegisterGenericRepositories = true;
    options.UseUnitOfWork<ISalesUnitOfWork, SalesUnitOfWork>();
});

builder.Services.AddRapidRepo(options =>
{
    options.UseEntityFrameworkCore<BillingDbContext>();
    options.ScanAssembliesContaining<BillingModuleMarker>();
    options.RegisterGenericRepositories = true;
    options.UseUnitOfWork<IBillingUnitOfWork, BillingUnitOfWork>();
});
```

Generic repositories bind per context, so `IRepository<Invoice, int>` resolves against whichever context declares `DbSet<Invoice>`.

### Entities mapped by more than one context

If two contexts both map an entity, `IRepository<Invoice, int>` cannot say which one is meant, because the service type carries no context. Rather than let the first registration win and point a later module at the wrong database, the second `AddRapidRepo` call throws:

```
Cannot register a generic repository for 'Invoice' against 'BillingDbContext':
'IRepository<Invoice, Int32>' is already registered against 'SalesDbContext'. ...
```

The check runs whenever `RegisterGenericRepositories` is on, so `RegisterAsSelf` does not avoid it. Turn `RegisterGenericRepositories` off in all but one of the calls whose contexts share the entity, and give those modules their repositories another way:

```csharp
// Give the module its own interface, and let scanning register it
public interface IBillingInvoiceRepository : IRepository<Invoice, int>;
public class BillingInvoiceRepository(BillingDbContext db)
    : EfRepository<Invoice, int>(db), IBillingInvoiceRepository;

// Or register the context-qualified concrete type yourself, and inject that
builder.Services.AddScoped<EfRepository<Invoice, int, BillingDbContext>>();
```

With `RegisterGenericRepositories` off, that call registers no generic repositories at all, for any entity of its context, and `RegisterAsSelf` adds no `EfRepository<TEntity, TId, TContext>` registrations either. Register every repository that module injects, by scanning or by hand.

### Repositories must name their own context

There is only one base `DbContext` service slot, so it cannot serve two contexts. In a multi-context application, write repositories and units of work against the derived type:

```csharp
// Good: unambiguous
public class SalesProductRepository(SalesDbContext db) : EfRepository<Product, long>(db), IProductRepository;

// Ambiguous across contexts
public class SalesProductRepository(DbContext db) : EfRepository<Product, long>(db), IProductRepository;
```

If two `AddRapidRepo` calls each register types needing the base `DbContext` for *different* contexts, the second call throws, rather than silently handing the second module the first module's context:

```
Cannot register the DbContext forwarder for 'BillingDbContext': the base DbContext service is
already forwarded to 'SalesDbContext'. ... Change them to take their own derived context type
(for example 'BillingDbContext') instead.
```

When every repository names its own context, no forwarder is registered at all and the conflict cannot arise.
