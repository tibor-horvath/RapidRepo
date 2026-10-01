# RapidRepo.SourceGenerators

A source generator for [RapidRepo](https://www.nuget.org/packages/RapidRepo) that writes your Unit of Work's repository properties for you. Mark a `partial` class with `[GenerateUnitOfWork]` and get a typed property for every `DbSet<T>` and custom repository, plus a matching interface, at build time.

## Installation

The attribute and the base types live in the core package, so add both:

```bash
dotnet add package RapidRepo
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
public partial class AppUnitOfWork : UnitOfWork<Guid>, IAppUnitOfWork
{
    public AppUnitOfWork(AppDbContext db, IServiceProvider sp)
        : base(db, Guid.Empty, sp) { }
}

public interface IAppUnitOfWork : IUnitOfWork<Guid>, IDisposable, IAppUnitOfWorkRepositories
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

- Property names come from the `DbSet<T>` member, or from the repository interface (`IMatchRepository` becomes `Matches`).
- A custom repository wins over a `DbSet<T>` of the same entity.
- `[UnitOfWorkProperty("People")]` on an interface overrides the generated name.

## Documentation

- [Source generator guide](https://github.com/tibor-horvath/RapidRepo/blob/master/docs/RapidRepo.SourceGenerators/unit-of-work.md)
- [RapidRepo documentation](https://github.com/tibor-horvath/RapidRepo#documentation)

Source and issues: [github.com/tibor-horvath/RapidRepo](https://github.com/tibor-horvath/RapidRepo). Licensed under MIT.
