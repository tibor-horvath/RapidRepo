# Dependency Injection

`RapidRepo.Extensions.DependencyInjection` provides an `IServiceCollection.AddRapidRepo(...)` extension that auto-discovers and registers repositories and the unit of work by scanning assemblies.

## Installation

```bash
dotnet add package RapidRepo.Extensions.DependencyInjection
```

No extra `using` directive is needed — the extension method lives in the `Microsoft.Extensions.DependencyInjection` namespace.

### Companion package — source generator (optional)

To eliminate repository property boilerplate from your Unit of Work class, also add the source generator:

```bash
dotnet add package RapidRepo.SourceGenerators
```

The package reference must use analyzer metadata so the generator DLL is not treated as a regular assembly:

```xml
<PackageReference Include="RapidRepo.SourceGenerators" Version="..."
    PrivateAssets="all"
    OutputItemType="Analyzer"
    ReferenceOutputAssembly="false" />
```

See [RapidRepo.SourceGenerators — Unit of Work](../RapidRepo.SourceGenerators/unit-of-work.md) for usage details.

---

## Quick start

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddRapidRepo(options =>
{
    options.UseDbContext<AppDbContext>();
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
| `RegisterAsSelf` | `bool` | `false` | Also registers each concrete type against itself, including `Repository<TEntity, TId, TContext>` when `UseDbContext` is set. |
| `ThrowOnAmbiguousRegistration` | `bool` | `true` | Throw when two concretes implement the same user-defined interface. |
| `ThrowOnSingletonMisuse` | `bool` | `false` | Throw (instead of warn) when `Lifetime` is `Singleton`. |
| `ThrowOnMissingDbContext` | `bool` | `false` | Throw (instead of warn) when registered types need a base `DbContext` that nothing provides. |
| `RegisterGenericRepositories` | `bool` | `false` | Register generic repositories for all three root interfaces. Open-generic by default; closed per entity when `UseDbContext` is set. |
| `UseDbContext<TContext>()` | method | — | Bind this call to `TContext`. Registers the `DbContext` forwarder when needed, and binds generic repositories to that context. |
| `ScanAssemblies(params Assembly[])` | method | — | Add one or more assemblies to scan. |
| `ScanAssembliesContaining<TMarker>()` | method | — | Add the assembly that contains `TMarker`. |
| `ScanCallingAssembly()` | method | — | Add the calling assembly (see note below). |
| `UseUnitOfWork<TInterface, TImpl>()` | method | — | Register a unit of work against an explicit interface. |
| `UseUnitOfWork<TImpl>()` | method | — | Register a unit of work by auto-detecting the single user-defined `IUnitOfWork<>` interface. Throws `ArgumentException` if `TImpl` is abstract or an interface. Throws `InvalidOperationException` when zero or more than one candidate interface is found. |
| `Include(Func<Type, bool>)` | method | — | Additive include predicate. |
| `Exclude(Func<Type, bool>)` | method | — | Additive exclude predicate. |
| `IncludeNamespace(string)` | method | — | Shorthand for `Include` matching types whose namespace starts with the given prefix. `null` throws `ArgumentNullException`. |
| `ExcludeNamespace(string)` | method | — | Shorthand for `Exclude` matching types whose namespace starts with the given prefix. `null` throws `ArgumentNullException`. |
| `IncludeType<T>()` | method | — | Shorthand for `Include(t => t == typeof(T))`. |
| `ExcludeType<T>()` | method | — | Shorthand for `Exclude(t => t == typeof(T))`. |

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

## Generic repositories

Set `RegisterGenericRepositories = true` to register `Repository<,>` as an open-generic fallback for all three root interfaces. This lets you inject `IRepository<TEntity, TKey>`, `IReadOnlyRepository<TEntity, TKey>`, or `IWriteRepository<TEntity, TKey>` directly, without writing a custom class.

```csharp
builder.Services.AddRapidRepo(options =>
{
    options.RegisterGenericRepositories = true;
    // ScanAssembliesContaining is still needed if you have custom repositories
});
```

**Precedence**: specific registrations from assembly scanning always win over the open-generic fallback. An application that scans a custom `ProductRepository : IProductRepository` will continue to resolve `IProductRepository → ProductRepository`; injecting `IRepository<Product, long>` in the same application resolves via the generic fallback to `Repository<Product, long>`.

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

### Namespace and type shortcuts

The `IncludeNamespace`, `ExcludeNamespace`, `IncludeType<T>`, and `ExcludeType<T>` helpers reduce common filter lambdas to a single readable call:

```csharp
// Equivalent to: options.Include(t => t.Namespace?.StartsWith("MyApp.Sales", ...) == true)
options.IncludeNamespace("MyApp.Sales");

// Equivalent to: options.Exclude(t => t.Namespace?.StartsWith("MyApp.Legacy", ...) == true)
options.ExcludeNamespace("MyApp.Legacy");

// Equivalent to: options.Exclude(t => t == typeof(LegacyOrderRepository))
options.ExcludeType<LegacyOrderRepository>();
```

`IncludeNamespace` / `ExcludeNamespace` use `StartsWith` with `Ordinal` comparison. Because `"MyApp.DataV2"` starts with `"MyApp.Data"`, the prefix `"MyApp.Data"` **does** match `"MyApp.DataV2"`. Add a trailing dot to enforce namespace-segment boundaries:

```csharp
options.ExcludeNamespace("MyApp.Data.");  // excludes MyApp.Data.Orders — does NOT match MyApp.DataV2
```

Passing `null` throws `ArgumentNullException` immediately at the call site.

---

## Unit of Work

For how to define and implement a Unit of Work, see [RapidRepo — Unit of Work](../RapidRepo/unit-of-work.md).

### Two-type overload (explicit)

```csharp
options.UseUnitOfWork<IAppUnitOfWork, AppUnitOfWork>();
```

This registers:
- `IAppUnitOfWork → AppUnitOfWork`
- `IUnitOfWork<TKey> → AppUnitOfWork` for every closed `IUnitOfWork<TKey>` that `AppUnitOfWork` implements

### Single-type overload (auto-detect)

When your implementation has exactly one user-defined `IUnitOfWork<TKey>`-derived interface, the interface can be omitted:

```csharp
options.UseUnitOfWork<AppUnitOfWork>();
```

This is equivalent to the two-type form above. Startup throws in the following cases:

| Situation | Exception |
|---|---|
| `TImpl` is abstract or an interface | `ArgumentException` |
| `TImpl` has no user-defined `IUnitOfWork<>` interface | `InvalidOperationException` |
| `TImpl` has more than one user-defined `IUnitOfWork<>` interface | `InvalidOperationException` |
| The detected interface was already registered | `InvalidOperationException` |

Call `UseUnitOfWork` once per unit of work interface type. Registering the same interface twice — whether via the same overload or by mixing both overloads — throws `InvalidOperationException`.

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

## `UseDbContext`

`AddDbContext<AppDbContext>()` registers `AppDbContext` — it does **not** register the base `DbContext` type. Repositories and units of work that take a `DbContext` constructor parameter therefore cannot be activated by the container:

```
System.InvalidOperationException: Unable to resolve service for type
'Microsoft.EntityFrameworkCore.DbContext' while attempting to activate 'ProductRepository'.
```

`UseDbContext<TContext>()` closes that gap:

```csharp
builder.Services.AddRapidRepo(options =>
{
    options.UseDbContext<AppDbContext>();
    options.ScanAssembliesContaining<ProductRepository>();
});
```

It does two things:

1. **Registers a `DbContext` → `TContext` forwarder**, but only when something registered by this call actually takes a base `DbContext` parameter. The forwarder hands out the same instance `AddDbContext` created, so repositories and the unit of work share one change tracker.
2. **Binds generic repositories to `TContext`.** With `RegisterGenericRepositories = true`, instead of one open-generic fallback you get a closed registration per eligible `DbSet<TEntity>` on `TContext`, backed by `Repository<TEntity, TId, TContext>`.

An entity is eligible when both hold:

- **It derives from `BaseEntity<TId>`.** RapidRepo repositories are defined in terms of that base, so a `DbSet<T>` for a plain EF entity is skipped — no repository is registered and no error is raised.
- **It has a `DbSet<>` of its own.** Discovery reflects over `DbSet<>` properties (public or not, matching how EF finds them) rather than building the model, so an entity EF reaches only through a navigation is not seen.

For anything that falls outside those rules, register a repository for it explicitly.

If you omit `UseDbContext` and nothing else registers a `DbContext`, `AddRapidRepo` writes a `Trace` warning. It does not throw by default, because an application is free to register its `DbContext` *after* calling `AddRapidRepo`, and that ordering cannot be distinguished at registration time. Set `ThrowOnMissingDbContext = true` to make it fail fast.

An application that already registers its own forwarder keeps it — `UseDbContext` will not overwrite it:

```csharp
builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<AppDbContext>());
```

---

## Multi-DbContext applications

Call `AddRapidRepo` once per bounded context, each naming its own context and scoped to its own assemblies and unit of work:

```csharp
builder.Services.AddDbContext<SalesDbContext>(/* ... */);
builder.Services.AddDbContext<BillingDbContext>(/* ... */);

builder.Services.AddRapidRepo(options =>
{
    options.UseDbContext<SalesDbContext>();
    options.ScanAssembliesContaining<SalesModuleMarker>();
    options.UseUnitOfWork<ISalesUnitOfWork, SalesUnitOfWork>();
});

builder.Services.AddRapidRepo(options =>
{
    options.UseDbContext<BillingDbContext>();
    options.ScanAssembliesContaining<BillingModuleMarker>();
    options.UseUnitOfWork<IBillingUnitOfWork, BillingUnitOfWork>();
});
```

Generic repositories bind per context, so `IRepository<Invoice, int>` resolves against whichever context declares `DbSet<Invoice>`.

### Entities mapped by more than one context

If two contexts both map an entity, `IRepository<Invoice, int>` cannot say which one is meant — the service type carries no context. Rather than let the first registration win and point a later module at the wrong database, the second `AddRapidRepo` call throws:

```
Cannot register a generic repository for 'Invoice' against 'BillingDbContext':
'IRepository<Invoice, Int32>' is already registered against 'SalesDbContext'. ...
```

Two ways forward:

```csharp
// Give each context its own interface, and let scanning register it
public interface ISalesInvoiceRepository : IRepository<Invoice, int>;
public class SalesInvoiceRepository(SalesDbContext db)
    : BaseRepository<Invoice, int>(db), ISalesInvoiceRepository;

// Or expose the context-qualified concrete type and inject that
options.RegisterAsSelf = true;   // registers Repository<Invoice, int, SalesDbContext>
```

`RegisterAsSelf` is unambiguous by construction here, since the context is part of the type.

### Repositories must name their own context

There is only one base `DbContext` service slot, so it cannot serve two contexts. In a multi-context application, write repositories and units of work against the derived type:

```csharp
// Good — unambiguous
public class SalesProductRepository(SalesDbContext db) : BaseRepository<Product, long>(db), IProductRepository;

// Ambiguous across contexts
public class SalesProductRepository(DbContext db) : BaseRepository<Product, long>(db), IProductRepository;
```

`BaseRepository` and `UnitOfWork` both take a `DbContext`, so passing a derived context to the base constructor needs no extra work.

If two `AddRapidRepo` calls each register types needing the base `DbContext` for *different* contexts, the second call throws rather than silently handing the second module the first module's context:

```
Cannot register the DbContext forwarder for 'BillingDbContext': the base DbContext service is
already forwarded to 'SalesDbContext'. ... Change them to take their own derived context type
(for example 'BillingDbContext') instead.
```

When every repository names its own context, no forwarder is registered at all and the conflict cannot arise.

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
