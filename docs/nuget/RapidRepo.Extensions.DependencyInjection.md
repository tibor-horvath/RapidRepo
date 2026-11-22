# RapidRepo.Extensions.DependencyInjection

Registers [RapidRepo](https://www.nuget.org/packages/RapidRepo) repositories and your Unit of Work with one `AddRapidRepo(...)` call, instead of one `AddScoped<>` line per repository.

## Installation

```bash
dotnet add package RapidRepo.Extensions.DependencyInjection
```

This package depends on `RapidRepo`, so you don't need to add the core package separately. EF Core applications get it through `RapidRepo.EntityFrameworkCore`, which also provides `UseEntityFrameworkCore<TContext>()`. The extension method lives in the `Microsoft.Extensions.DependencyInjection` namespace, so no extra `using` is needed.

## Quick start

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddRapidRepo(options =>
{
    options.UseEntityFrameworkCore<AppDbContext>();   // from RapidRepo.EntityFrameworkCore
    options.ScanAssembliesContaining<ProductRepository>();
    options.RegisterGenericRepositories = true;
    options.UseUnitOfWork<AppUnitOfWork>(); // interface auto-detected
});
```

- Every concrete class implementing an interface derived from `IRepository<,>`, `IReadOnlyRepository<,>` or `IWriteRepository<,>` in the scanned assemblies is registered.
- `RegisterGenericRepositories` lets you inject `IRepository<Product, long>` directly, with no custom repository class. The provider supplies the implementation.
- The provider (`UseEntityFrameworkCore<TContext>()` for EF Core) adds what depends on the persistence technology, such as forwarding the base `DbContext` when a repository needs it.

## Options at a glance

| Option | Default | Purpose |
|---|---|---|
| `Lifetime` | `Scoped` | Lifetime of discovered repositories |
| `RegisterAsSelf` | `false` | Also register each concrete type against itself |
| `ThrowOnAmbiguousRegistration` | `true` | Fail when two classes implement the same interface |
| `RegisterGenericRepositories` | `false` | Let the provider register its generic repository; requires a provider |
| `Include` / `Exclude`, `IncludeNamespace` / `ExcludeNamespace`, `IncludeType<T>` / `ExcludeType<T>` | — | Filter which types are registered |
| `UseCurrentUserProvider<TProvider, TUserKey>()` / `UseCurrentUser<TUserKey>(...)` | — | Supply the audit user, so commits don't have to pass one |
| `RequireCurrentUser` | `false` | Throw instead of stamping the default user key when a commit has no user |
| `UseCurrentTenantProvider<TProvider, TTenantKey>()` / `UseCurrentTenant<TTenantKey>(...)` | — | Supply the current tenant for multi-tenancy, and register the tenant scopes |
| `ValidateTenancyOnStartup` | `true` | With a tenant provider, validate the tenancy setup when the host starts |

## Documentation

- [Dependency injection guide and full options reference](https://github.com/tibor-horvath/RapidRepo/blob/master/docs/RapidRepo.Extensions.DependencyInjection/dependency-injection.md)
- [EF Core registration](https://github.com/tibor-horvath/RapidRepo/blob/master/docs/RapidRepo.EntityFrameworkCore/ef-core-registration.md)
- [RapidRepo documentation](https://github.com/tibor-horvath/RapidRepo#documentation)

Pairs well with [RapidRepo.SourceGenerators](https://www.nuget.org/packages/RapidRepo.SourceGenerators), which generates the repository properties of your Unit of Work.

Source and issues: [github.com/tibor-horvath/RapidRepo](https://github.com/tibor-horvath/RapidRepo). Licensed under MIT.
