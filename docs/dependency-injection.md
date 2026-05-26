# Dependency Injection

`RapidRepo.Extensions.DependencyInjection` provides an `IServiceCollection.AddRapidRepo(...)` extension that auto-discovers and registers repositories and the unit of work by scanning assemblies.

## Installation

```bash
dotnet add package RapidRepo.Extensions.DependencyInjection
```

No extra `using` directive is needed — the extension method lives in the `Microsoft.Extensions.DependencyInjection` namespace.

---

## Quick start

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddRapidRepo(options =>
{
    options.ScanAssembliesContaining<ProductRepository>();
    options.UseUnitOfWork<IAppUnitOfWork, AppUnitOfWork>();
});
```

This single call replaces one `AddScoped<>` line per repository. Any concrete, non-abstract class that implements a user-defined interface derived from `IReadOnlyRepository<,>`, `IWriteRepository<,>`, or `IRepository<,>` is registered automatically.

---

## Options reference

| Member | Type | Default | Description |
|---|---|---|---|
| `Lifetime` | `ServiceLifetime` | `Scoped` | Lifetime applied to all discovered repositories. |
| `RegisterAsSelf` | `bool` | `false` | Also registers each concrete type against itself. |
| `ThrowOnAmbiguousRegistration` | `bool` | `true` | Throw when two concretes implement the same user-defined interface. |
| `ThrowOnSingletonMisuse` | `bool` | `false` | Throw (instead of warn) when `Lifetime` is `Singleton`. |
| `ScanAssemblies(params Assembly[])` | method | — | Add one or more assemblies to scan. |
| `ScanAssembliesContaining<TMarker>()` | method | — | Add the assembly that contains `TMarker`. |
| `ScanCallingAssembly()` | method | — | Add the calling assembly (see note below). |
| `UseUnitOfWork<TInterface, TImpl>()` | method | — | Register a unit of work implementation. |
| `Include(Func<Type, bool>)` | method | — | Additive include predicate. |
| `Exclude(Func<Type, bool>)` | method | — | Additive exclude predicate. |

---

## Assembly scanning

### `ScanAssembliesContaining<TMarker>` (preferred)

```csharp
options.ScanAssembliesContaining<ProductRepository>();
```

Adds the assembly that contains `ProductRepository` to the scan list. This is the recommended form — it is unambiguous regardless of how the application is hosted or tested.

### `ScanAssemblies`

```csharp
options.ScanAssemblies(
    typeof(MyApp.Sales.ProductRepository).Assembly,
    typeof(MyApp.Billing.InvoiceRepository).Assembly);
```

### `ScanCallingAssembly`

```csharp
options.ScanCallingAssembly();
```

Resolves the assembly at the call site. Works in `Program.cs` but can be unreliable inside lambdas or test harnesses. Prefer `ScanAssembliesContaining<T>()` when in doubt.

---

## Discovery rules

A type is registered if all of the following are true:

1. It is concrete (not abstract), not an interface, not an open generic, and not compiler-generated.
2. It implements at least one closed generic of `IReadOnlyRepository<,>`, `IWriteRepository<,>`, or `IRepository<,>`.
3. No `Exclude` predicate returns `true`.
4. Either no `Include` predicates are configured, or at least one `Include` predicate returns `true`.

**Interface registration policy**: each concrete type is registered against every public interface it implements that itself derives (directly or transitively) from one of the three root interfaces — excluding the root interfaces themselves. This targets user-defined interfaces such as `IProductRepository`.

---

## Filters

Include and exclude predicates are additive. Exclude always wins over include.

```csharp
// Only register repositories in the Sales module
options.Include(t => t.Namespace?.StartsWith("MyApp.Sales.") == true);

// Skip legacy and experimental repositories
options.Exclude(t => t.Name.StartsWith("Legacy"));
options.Exclude(t => t.Namespace?.Contains(".Experimental") == true);
```

---

## Unit of Work

```csharp
options.UseUnitOfWork<IAppUnitOfWork, AppUnitOfWork>();
```

This registers:
- `IAppUnitOfWork → AppUnitOfWork`
- `IUnitOfWork<TKey> → AppUnitOfWork` for every closed `IUnitOfWork<TKey>` that `AppUnitOfWork` implements

Call `UseUnitOfWork` once per unit of work interface type. Registering the same interface twice throws `InvalidOperationException`.

---

## Lifetime

The default `ServiceLifetime.Scoped` matches the lifetime of `DbContext` in ASP.NET Core. Changing to `Transient` is safe but allocates more. `Singleton` causes captive-dependency issues — by default a warning is written to `Debug`; set `ThrowOnSingletonMisuse = true` to throw instead.

```csharp
options.Lifetime = ServiceLifetime.Transient;
```

---

## Idempotency

Calling `AddRapidRepo` twice with the same assembly does not duplicate descriptors. Registrations use `TryAddEnumerable`, which skips any descriptor whose `(ServiceType, ImplementationType)` pair already exists.

---

## Multi-DbContext applications

Call `AddRapidRepo` once per bounded context, each scoped to its own assemblies and unit of work:

```csharp
builder.Services.AddDbContext<SalesDbContext>(/* ... */);
builder.Services.AddDbContext<BillingDbContext>(/* ... */);

builder.Services.AddRapidRepo(options =>
{
    options.ScanAssembliesContaining<SalesModuleMarker>();
    options.UseUnitOfWork<ISalesUnitOfWork, SalesUnitOfWork>();
});

builder.Services.AddRapidRepo(options =>
{
    options.ScanAssembliesContaining<BillingModuleMarker>();
    options.UseUnitOfWork<IBillingUnitOfWork, BillingUnitOfWork>();
});
```

---

## Migration from manual registration

Replace individual `AddScoped<>` calls:

```csharp
// Before
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IAppUnitOfWork, AppUnitOfWork>();

// After
builder.Services.AddRapidRepo(options =>
{
    options.ScanAssembliesContaining<ProductRepository>();
    options.UseUnitOfWork<IAppUnitOfWork, AppUnitOfWork>();
});
```

Manual registrations added before `AddRapidRepo` are preserved; `TryAddEnumerable` will not overwrite them.
