# RapidRepo.SourceGenerators

A source generator for [RapidRepo](https://www.nuget.org/packages/RapidRepo) that writes your Unit of Work's repository properties for you. Mark a `partial` class with `[GenerateUnitOfWork]` and get a typed property for every `DbSet<T>`, entity type and custom repository, plus a matching interface, at build time.

## Installation

The attribute lives in the core package, and the unit of work base class comes from your provider. For EF Core:

```bash
dotnet add package RapidRepo.EntityFrameworkCore
dotnet add package RapidRepo.SourceGenerators
```

Reference the generator as an analyzer:

```xml
<PackageReference Include="RapidRepo.SourceGenerators" Version="..."
    PrivateAssets="all"
    OutputItemType="Analyzer"
    ReferenceOutputAssembly="false" />
```

## Usage

```csharp
[GenerateUnitOfWork(typeof(AppDbContext))]
public partial class AppUnitOfWork : EfUnitOfWork<Guid>, IAppUnitOfWork
{
    public AppUnitOfWork(AppDbContext db, IServiceProvider sp)
        : base(db, Guid.Empty, sp) { }
}

public interface IAppUnitOfWork : IUnitOfWork<Guid>, IAppUnitOfWorkRepositories
{
}
```

The generator emits `IAppUnitOfWorkRepositories` and the matching properties:

```csharp
public partial class AppUnitOfWork
{
    public IRepository<User, Guid> Users   => GetRepository<User, Guid>();
    public IMatchRepository        Matches => ResolveRepository<IMatchRepository>();
}
```

- Sources can be `DbContext` types or entity types: `[GenerateUnitOfWork(typeof(Product), typeof(Order))]` works without a `DbContext`, for providers that have none.
- Property names come from the `DbSet<T>` member, the entity name in plural (`Category` becomes `Categories`), or the repository interface (`IMatchRepository` becomes `Matches`).
- A custom repository wins over a `DbSet<T>` or entity source of the same entity.
- `[UnitOfWorkProperty("People")]` on an interface overrides the generated name.

## Documentation

- [Source generator guide](https://github.com/tibor-horvath/RapidRepo/blob/master/docs/RapidRepo.SourceGenerators/generate-unit-of-work.md)
- [RapidRepo documentation](https://github.com/tibor-horvath/RapidRepo#documentation)

Source and issues: [github.com/tibor-horvath/RapidRepo](https://github.com/tibor-horvath/RapidRepo). Licensed under MIT.
