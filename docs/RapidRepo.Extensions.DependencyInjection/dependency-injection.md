# Dependency Injection

`RapidRepo.Extensions.DependencyInjection` provides an `IServiceCollection.AddRapidRepo(...)` extension that auto-discovers and registers repositories and the unit of work by scanning assemblies.

The package is provider-neutral. What depends on the persistence technology, such as the generic repository implementation and its data source, comes from a provider you configure in the same call, for example `UseEntityFrameworkCore<TContext>()` from [RapidRepo.EntityFrameworkCore](../RapidRepo.EntityFrameworkCore/ef-core-registration.md).

## Installation

```bash
dotnet add package RapidRepo.Extensions.DependencyInjection
```

EF Core applications get this package through `RapidRepo.EntityFrameworkCore`, which depends on it.

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

See [RapidRepo.SourceGenerators — Unit of Work](../RapidRepo.SourceGenerators/generate-unit-of-work.md) for usage details.

---

## Quick start

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddRapidRepo(options =>
{
    options.UseEntityFrameworkCore<AppDbContext>();
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
| `RegisterAsSelf` | `bool` | `false` | Also registers each concrete type against itself. Providers may register their own types against themselves too. |
| `ThrowOnAmbiguousRegistration` | `bool` | `true` | Throw when two concretes implement the same user-defined interface. |
| `ThrowOnSingletonMisuse` | `bool` | `false` | Throw (instead of warn) when `Lifetime` is `Singleton`. |
| `RegisterGenericRepositories` | `bool` | `false` | Have the provider register its generic repository for the root interfaces. Requires a provider. |
| `RequireCurrentUser` | `bool` | `false` | A commit with no audit user throws when it would stamp `CreatedBy`, `ModifiedBy` or `DeletedBy`, instead of stamping the default user key. See [Current user](#current-user). |
| `UseCurrentUserProvider<TProvider, TUserKey>()` | method | — | Register the `ICurrentUserProvider<TUserKey>` units of work ask for the audit user. One per user key type. |
| `UseCurrentUser<TUserKey>(Func<IServiceProvider, TUserKey?>)` | method | — | Same, from a delegate. |
| `UseCurrentTenantProvider<TProvider, TTenantKey>()` | method | — | Register the `ICurrentTenantProvider<TTenantKey>` that supplies the request's tenant, the tenant scopes and the startup validation. One per tenant key type. See [Current tenant](#current-tenant). |
| `UseCurrentTenant<TTenantKey>(Func<IServiceProvider, TTenantKey?>)` | method | — | Same, from a delegate. |
| `ValidateTenancyOnStartup` | `bool` | `true` | With a tenant provider, run the tenancy validation in a hosted service at startup. |
| `UseProvider(IRapidRepoProvider)` | method | — | Configure the provider for this call. Provider packages wrap it, e.g. `UseEntityFrameworkCore<TContext>()`. One per call. |
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
2. It implements at least one closed generic of `IReadOnlyRepository<,>`, `IWriteRepository<,>`, `IRepository<,>`, or a root interface of the configured provider (such as `IEfRepository<,>`).
3. No `Exclude` predicate returns `true`.
4. Either no `Include` predicates are configured, or at least one `Include` predicate returns `true`.

**Interface registration policy**: each concrete type is registered against every public interface it implements that itself derives (directly or transitively) from one of the root interfaces, excluding the root interfaces themselves. This targets user-defined interfaces such as `IProductRepository`.

The root interfaces are the three core contracts plus the provider's own, for example `IEfRepository<,>` and `IEfReadOnlyRepository<,>` for EF Core. Scan provider-based repositories **with the provider configured**. Otherwise its interfaces count as user-defined, and two repositories for the same entity become ambiguous; the error message then suggests configuring the provider.

---

## Generic repositories

Set `RegisterGenericRepositories = true` to have the provider register its generic repository for the root interfaces. You can then inject `IRepository<TEntity, TKey>`, `IReadOnlyRepository<TEntity, TKey>`, or `IWriteRepository<TEntity, TKey>` directly, without writing a custom class.

```csharp
builder.Services.AddRapidRepo(options =>
{
    options.UseEntityFrameworkCore<AppDbContext>();
    options.RegisterGenericRepositories = true;
});
```

The provider decides how. EF Core registers one repository per `DbSet<>` when the call is bound to a context, and an open-generic fallback otherwise; see [EF Core registration](../RapidRepo.EntityFrameworkCore/ef-core-registration.md). Without a provider, `RegisterGenericRepositories` throws, because there is no implementation to register.

**Precedence**: specific registrations from assembly scanning always win. An application that scans a custom `ProductRepository : IProductRepository` resolves `IProductRepository` to `ProductRepository`, and `IRepository<Product, long>` to the provider's generic repository.

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

## Current user

Register an `ICurrentUserProvider<TUserKey>` so that units of work stamp the audit user without every commit passing one. For the resolution order and how a unit of work finds the provider, see [Auditing — The current user](../RapidRepo/auditing.md#the-current-user).

```csharp
builder.Services.AddRapidRepo(options =>
{
    options.UseEntityFrameworkCore<AppDbContext>();
    options.UseUnitOfWork<AppUnitOfWork>();
    options.UseCurrentUserProvider<HttpContextUserProvider, Guid>();
});
```

For a one-liner, use a delegate. It runs with the service provider of the unit of work's scope:

```csharp
options.UseCurrentUser<Guid>(sp =>
    Guid.TryParse(sp.GetRequiredService<IHttpContextAccessor>().HttpContext?.User
        .FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null);
```

- The provider is registered with `Lifetime`, the lifetime of the units of work, so a scoped unit of work gets a scoped provider that can depend on per-request services. With a `Singleton` lifetime, the singleton misuse warning or error mentions the provider too.
- Configure one provider per user key type; a second `UseCurrentUserProvider` or `UseCurrentUser` for the same type throws `InvalidOperationException`.
- A registration the application already made for `ICurrentUserProvider<TUserKey>` takes precedence.

Set `RequireCurrentUser = true` to make a commit with no user throw, instead of stamping the default user key, when it would stamp a user field. It registers `CurrentUserOptions`, which the unit of work reads from the service provider passed to its constructor.

> **The unit of work must take an `IServiceProvider`.** Both the current-user provider and `RequireCurrentUser` reach the unit of work through the `IServiceProvider` passed to its base constructor, for example `EfUnitOfWork<Guid>(db, Guid.Empty, serviceProvider)`. A unit of work constructed without one ignores these settings without an error, and stamps the default user key.

---

## Current tenant

Register an `ICurrentTenantProvider<TTenantKey>` for [multi-tenancy](../RapidRepo/multi-tenancy.md):

```csharp
builder.Services.AddRapidRepo(options =>
{
    options.UseEntityFrameworkCore<AppDbContext>();
    options.UseCurrentTenantProvider<ClaimsTenantProvider, Guid>();
    // or: options.UseCurrentTenant<Guid>(sp => /* read a claim or header */);
});
```

Configuring a tenant provider also registers:

- the provider itself, with `Lifetime`, like the current-user provider. Configure one per tenant key type; a registration the application already made takes precedence.
- `ITenantScope<TTenantKey>` and `ITenantHostScope`, which log every override and host scope. `ITenantHostScope` applies an `ITenantHostScopePolicy`, if you register one.
- a hosted service that runs the tenancy validation when the host starts, so `host.StartAsync()` fails on a misconfiguration with a `RapidRepoConfigurationException` listing every problem in `Problems`. Set `ValidateTenancyOnStartup = false` to leave it out, for example when a test calls `serviceProvider.ValidateRapidRepoTenancy()` instead. The persistence provider's validation on first use still runs.

Without a tenant provider, `AddRapidRepo` registers none of these, and nothing resolves your context at startup.

---

## Lifetime

The default `ServiceLifetime.Scoped` matches the lifetime of the data access resources repositories depend on, such as an EF Core `DbContext` in ASP.NET Core. Changing to `Transient` is safe but allocates more. `Singleton` causes captive-dependency issues: by default a warning is written to `Trace`; set `ThrowOnSingletonMisuse = true` to throw instead.

```csharp
options.Lifetime = ServiceLifetime.Transient;
```

---

## Idempotency

Calling `AddRapidRepo` twice with the same assembly does not duplicate descriptors. Registrations use `TryAddEnumerable`, which skips any descriptor whose `(ServiceType, ImplementationType)` pair already exists.

---

## Providers

A provider implements `IRapidRepoProvider` and is configured through an extension of its own on `RapidRepoOptions`, which calls `UseProvider`:

| Provider package | Extension |
|---|---|
| `RapidRepo.EntityFrameworkCore` | `UseEntityFrameworkCore<TContext>()`, `UseEntityFrameworkCore()`; see [EF Core registration](../RapidRepo.EntityFrameworkCore/ef-core-registration.md) |

`AddRapidRepo` scans, filters, checks for ambiguity, and registers the scanned repositories and units of work. It then hands the provider a `RapidRepoRegistrationContext`, which holds what was registered and how the call was configured, so the provider can add its own registrations. Configure one provider per call; use a separate `AddRapidRepo` call per provider or data source.

A call that registers repositories or units of work without a provider writes a warning to `Trace`: nothing then registers the data source they depend on, so unless the application registers it itself they fail on first resolve.

To write a provider, see [Writing a provider](../RapidRepo/providers.md).

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
    options.UseEntityFrameworkCore<AppDbContext>();
    options.ScanAssembliesContaining<ProductRepository>();
    options.UseUnitOfWork<IAppUnitOfWork, AppUnitOfWork>();
});
```

Manual registrations added before `AddRapidRepo` are preserved; `TryAddEnumerable` will not overwrite them.
