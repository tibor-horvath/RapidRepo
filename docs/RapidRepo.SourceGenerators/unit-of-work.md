# Unit of Work — Source Generator

`RapidRepo.SourceGenerators` eliminates all repository property boilerplate from your Unit of Work class. Add the attribute to a `partial` class and the generator emits the typed properties and their interface at build time.

For the manual approach to writing a Unit of Work, see [RapidRepo — Unit of Work](../RapidRepo/unit-of-work.md).

---

## Installation

`[GenerateUnitOfWork]`, `UnitOfWork<T>`, and the types the generator emits live in the core package. Add both:

```bash
dotnet add package RapidRepo
dotnet add package RapidRepo.SourceGenerators
```

Reference it as an analyzer in your `.csproj` (not a regular assembly):

```xml
<PackageReference Include="RapidRepo.SourceGenerators" Version="..."
    PrivateAssets="all"
    OutputItemType="Analyzer"
    ReferenceOutputAssembly="false" />
```

---

## Usage

```csharp
// AppUnitOfWork.cs — only file you maintain
[GenerateUnitOfWork(typeof(AppDbContext))]
public partial class AppUnitOfWork : UnitOfWork<Guid>, IAppUnitOfWork
{
    public AppUnitOfWork(AppDbContext db, IServiceProvider sp)
        : base(db, Guid.Empty, sp) { }
}

// IAppUnitOfWork.cs — slim interface that inherits the generated one
public interface IAppUnitOfWork : IUnitOfWork<Guid>, IDisposable, IAppUnitOfWorkRepositories
{
    // add only app-specific extras here
}
```

The generator emits two files at build time:

```csharp
// IAppUnitOfWorkRepositories.g.cs — generated
namespace MyApp.Data;

public interface IAppUnitOfWorkRepositories
{
    IRepository<User, Guid>   Users         { get; }   // from DbSet<User>
    IMatchRepository          Matches       { get; }   // custom repo, overrides DbSet<Match>
    IUserProviderRepository   UserProviders { get; }   // custom repo, no matching DbSet
}

// AppUnitOfWork.Repositories.g.cs — generated
namespace MyApp.Data;

public partial class AppUnitOfWork
{
    public IRepository<User, Guid>   Users         => GetRepository<User, Guid>();
    public IMatchRepository          Matches       => ResolveRepository<IMatchRepository>();
    public IUserProviderRepository   UserProviders => ResolveRepository<IUserProviderRepository>();
}
```

---

## Property naming

| Source | Name derivation |
|---|---|
| `DbSet<T> Users` | Member name as-is (`Users`) |
| `IMatchRepository` | Strip `I` + `Repository`, pluralize → `Matches` |
| `IUserProviderRepository` | Strip `I` + `Repository`, pluralize → `UserProviders` |
| `[UnitOfWorkProperty("People")]` on interface | Override to `People` |

Pluralization rules: consonant + `y` → `ies`; ends in `s/sh/ch/x/z` → `es`; otherwise → `s`.
For irregular plurals (`Person` → `People`), apply `[UnitOfWorkProperty("People")]` on the interface.

---

## Deduplication and capability narrowing

When a custom repo interface covers the same entity as a DbSet property, **the custom repo wins**. The generated property name is derived from the custom repo name, not the DbSet member name. If you relied on the DbSet name, apply `[UnitOfWorkProperty("DbSetName")]` on the interface.

If the custom repo extends `IReadOnlyRepository<,>` or `IWriteRepository<,>` instead of `IRepository<,>`, the generated property type reflects that narrower contract. Write or read methods will not be accessible through the UoW property — this is intentional.

---

## Attribute reference

| Property | Type | Default | Description |
|---|---|---|---|
| Constructor arg | `params Type[]` | — | One or more `DbContext` types to scan for `DbSet<T>` properties |
| `RepositoriesInterfaceName` | `string?` | `I{ClassName}Repositories` | Override the generated interface name |
| `IncludeNamespaces` | `string[]` | `[]` (all) | Only include custom repo interfaces from these namespace prefixes |
| `ExcludeNamespaces` | `string[]` | `[]` (none) | Exclude custom repo interfaces from these namespace prefixes |

---

## Diagnostics

| Code | Severity | Cause |
|---|---|---|
| `RRUOW001` | Warning | DbSet entity does not inherit `BaseEntity<TKey>` — property skipped |
| `RRUOW002` | Error | Two custom repos resolve to the same property name |
| `RRUOW003` | Error | Attributed class is not `partial` |
| `RRUOW004` | Error | Type passed to the attribute does not inherit `DbContext` |

---

## DI registration

Pass `IServiceProvider` to the base constructor so `ResolveRepository<>()` can resolve custom repo instances from the container:

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddRapidRepo(options =>
{
    options.ScanAssembliesContaining<ProductRepository>();
    options.UseUnitOfWork<IAppUnitOfWork, AppUnitOfWork>();
});
```

DbSet-backed properties (`GetRepository<TEntity, TKey>()`) are zero-DI — they never touch the container.
Custom repo properties (`ResolveRepository<TRepo>()`) are resolved from the `IServiceProvider` passed to the constructor.

See [Dependency Injection](../RapidRepo.Extensions.DependencyInjection/dependency-injection.md) for the full `AddRapidRepo` reference.
