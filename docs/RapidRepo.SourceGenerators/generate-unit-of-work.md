# Generated Unit of Work

`RapidRepo.SourceGenerators` eliminates all repository property boilerplate from your Unit of Work class. Add the attribute to a `partial` class and the generator emits the typed properties and their interface at build time.

For the manual approach to writing a Unit of Work, see [RapidRepo — Unit of Work](../RapidRepo/unit-of-work.md).

---

## Installation

`[GenerateUnitOfWork]` lives in the core package, and the unit of work base class comes from your provider. For EF Core:

```bash
dotnet add package RapidRepo.EntityFrameworkCore
dotnet add package RapidRepo.SourceGenerators
```

Reference the generator as an analyzer in your `.csproj` (not a regular assembly):

```xml
<PackageReference Include="RapidRepo.SourceGenerators" Version="..."
    PrivateAssets="all"
    OutputItemType="Analyzer"
    ReferenceOutputAssembly="false" />
```

The generator itself needs no EF Core reference; it recognises a `DbContext` by name.

---

## Usage

```csharp
// AppUnitOfWork.cs — the only file you maintain
[GenerateUnitOfWork(typeof(AppDbContext))]
public partial class AppUnitOfWork : EfUnitOfWork<Guid>, IAppUnitOfWork
{
    public AppUnitOfWork(AppDbContext db, IServiceProvider sp)
        : base(db, Guid.Empty, sp) { }
}

// IAppUnitOfWork.cs — slim interface that inherits the generated one
public interface IAppUnitOfWork : IUnitOfWork<Guid>, IAppUnitOfWorkRepositories
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

The generated code uses only `GetRepository` and `ResolveRepository`, which every unit of work inherits from the provider-neutral `UnitOfWorkBase<TUserKey>`. It brings its own `using` directives, so it also compiles in projects without implicit usings.

---

## Sources: DbContexts and entity types

Each type passed to the attribute is a **source** of entities:

| Source | Entities |
|---|---|
| A type deriving from `Microsoft.EntityFrameworkCore.DbContext` | Every public, non-static `DbSet<TEntity>` property whose entity derives from `BaseEntity<TId>` |
| A type deriving from `BaseEntity<TId>` | That entity |

Non-public `DbSet` properties are skipped. [`UseEntityFrameworkCore<TContext>()`](../RapidRepo.EntityFrameworkCore/ef-core-registration.md#useentityframeworkcoretcontext) does register a generic repository for their entities, but only with `options.RegisterGenericRepositories = true`, which is off by default; turn it on before injecting `IRepository<TEntity, TKey>` for such an entity. To get a property for such an entity, pass its type as a source as well, for example `[GenerateUnitOfWork(typeof(AppDbContext), typeof(AuditEntry))]`.

Entity sources are how providers without a `DbContext` use the generator. The two kinds can be mixed:

```csharp
// MyOrmUnitOfWork<Guid> stands for the unit-of-work base class of a provider without a DbContext.
[GenerateUnitOfWork(typeof(Product), typeof(Order), typeof(Category))]
public partial class CatalogUnitOfWork : MyOrmUnitOfWork<Guid>, ICatalogUnitOfWork { ... }

[GenerateUnitOfWork(typeof(AppDbContext), typeof(AuditEntry))]
public partial class AppUnitOfWork : EfUnitOfWork<Guid>, IAppUnitOfWork { ... }
```

When two sources supply the same entity, the first one wins.

---

## Property naming

| Source | Name derivation |
|---|---|
| `DbSet<T> Users` | Member name as-is (`Users`) |
| Entity type `Category` | Entity name, pluralized → `Categories` |
| `IMatchRepository` | Strip `I` + `Repository`, pluralize → `Matches` |
| `IUserProviderRepository` | Strip `I` + `Repository`, pluralize → `UserProviders` |
| `[UnitOfWorkProperty("People")]` on interface | Override to `People` |

Pluralization rules: consonant + `y` → `ies`; ends in `s/sh/ch/x/z` → `es`; otherwise → `s`. Only interface names lose a leading `I`, so an entity named `Item` becomes `Items`.
For irregular plurals (`Person` → `People`), declare a custom repository interface with `[UnitOfWorkProperty("People")]`.

---

## Deduplication and capability narrowing

When a custom repo interface covers the same entity as a DbSet property or an entity source, **the custom repo wins**. The generated property name then comes from the custom repo name, not the DbSet member name. If you relied on the DbSet name, apply `[UnitOfWorkProperty("DbSetName")]` on the interface.

If the custom repo extends `IReadOnlyRepository<,>` or `IWriteRepository<,>` instead of `IRepository<,>`, the generated property type reflects that narrower contract. Write or read methods are then not accessible through the UoW property; this is intentional.

---

## Attribute reference

| Member | Type | Default | Description |
|---|---|---|---|
| Constructor arg `sources` | `params Type[]` | — | DbContext types and entity types; exposed as the `Sources` property |
| `RepositoriesInterfaceName` | `string?` | `I{ClassName}Repositories` | Override the generated interface name |
| `IncludeNamespaces` | `string[]` | `[]` (all) | Only include custom repo interfaces from these namespace prefixes |
| `ExcludeNamespaces` | `string[]` | `[]` (none) | Exclude custom repo interfaces from these namespace prefixes |

`IncludeNamespaces` and `ExcludeNamespaces` filter custom repository interfaces only; entities from the sources are always included.

---

## Diagnostics

| Code | Severity | Cause |
|---|---|---|
| `RRUOW001` | Warning | A `DbSet` entity does not inherit `BaseEntity<TKey>`; the property is skipped |
| `RRUOW002` | Error | Two repositories resolve to the same property name |
| `RRUOW003` | Error | The attributed class is not `partial` |
| `RRUOW004` | Error | A type passed to the attribute is neither a `DbContext` nor a `BaseEntity<TKey>` (open generic types included) |

---

## DI registration

Pass `IServiceProvider` to the base constructor so `ResolveRepository<>()` can resolve custom repo instances from the container:

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

Generic properties (`GetRepository<TEntity, TKey>()`) never touch the container; the provider creates the repository. Custom repo properties (`ResolveRepository<TRepo>()`) are resolved from the `IServiceProvider` passed to the constructor.

See [Dependency Injection](../RapidRepo.Extensions.DependencyInjection/dependency-injection.md) for the full `AddRapidRepo` reference.
