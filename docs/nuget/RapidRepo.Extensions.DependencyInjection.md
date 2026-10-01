# RapidRepo.Extensions.DependencyInjection

Registers [RapidRepo](https://www.nuget.org/packages/RapidRepo) repositories and your Unit of Work with one `AddRapidRepo(...)` call, instead of one `AddScoped<>` line per repository.

## Installation

```bash
dotnet add package RapidRepo.Extensions.DependencyInjection
```

This package depends on `RapidRepo`, so you don't need to add the core package separately. The extension method lives in the `Microsoft.Extensions.DependencyInjection` namespace, so no extra `using` is needed.

## Quick start

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddRapidRepo(options =>
{
    options.UseDbContext<AppDbContext>();
    options.ScanAssembliesContaining<ProductRepository>();
    options.RegisterGenericRepositories = true;
    options.UseUnitOfWork<AppUnitOfWork>(); // interface auto-detected
});
```

- Every concrete class implementing an interface derived from `IRepository<,>`, `IReadOnlyRepository<,>` or `IWriteRepository<,>` in the scanned assemblies is registered.
- `RegisterGenericRepositories` lets you inject `IRepository<Product, long>` directly, with no custom repository class.
- `UseDbContext<TContext>()` binds repositories to your context and forwards the base `DbContext` when a repository needs it.

## Options at a glance

| Option | Default | Purpose |
|---|---|---|
| `Lifetime` | `Scoped` | Lifetime of discovered repositories |
| `RegisterAsSelf` | `false` | Also register each concrete type against itself |
| `ThrowOnAmbiguousRegistration` | `true` | Fail when two classes implement the same interface |
| `Include` / `Exclude`, `IncludeNamespace` / `ExcludeNamespace`, `IncludeType<T>` / `ExcludeType<T>` | — | Filter which types are registered |

## Documentation

- [Dependency injection guide and full options reference](https://github.com/tibor-horvath/RapidRepo/blob/master/docs/RapidRepo.Extensions.DependencyInjection/dependency-injection.md)
- [RapidRepo documentation](https://github.com/tibor-horvath/RapidRepo#documentation)

Pairs well with [RapidRepo.SourceGenerators](https://www.nuget.org/packages/RapidRepo.SourceGenerators), which generates the repository properties of your Unit of Work.

Source and issues: [github.com/tibor-horvath/RapidRepo](https://github.com/tibor-horvath/RapidRepo). Licensed under MIT.
