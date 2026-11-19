using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RapidRepo.Auditing;
using RapidRepo.Entities;
using RapidRepo.Extensions;
using RapidRepo.Repositories.Interfaces;
using RapidRepo.Testing.Conformance;
using RapidRepo.UnitOfWork;

namespace RapidRepo.EntityFrameworkCore.Tests.Conformance;

/// <summary>
/// The conformance entities mapped the way an application would map them: soft-deleted notes filtered out by
/// <see cref="ModelBuilderExtensions.AddSoftDeleteQueryFilters"/>.
/// </summary>
public sealed class ConformanceDbContext(DbContextOptions<ConformanceDbContext> options) : DbContext(options)
{
    public DbSet<Widget> Widgets => Set<Widget>();

    public DbSet<Note> Notes => Set<Note>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Widget>();
        modelBuilder.Entity<Note>();
        modelBuilder.AddSoftDeleteQueryFilters();
    }
}

/// <summary>A session: one context, its unit of work, and repositories over that context.</summary>
public sealed class EfConformanceSession : IConformanceSession
{
    private readonly ConformanceDbContext _dbContext;

    public EfConformanceSession(ConformanceDbContext dbContext, TimeProvider clock, ICurrentUserProvider<Guid>? currentUserProvider)
    {
        _dbContext = dbContext;
        UnitOfWork = new ConformanceUnitOfWork(dbContext, clock, currentUserProvider);
    }

    public IUnitOfWork<Guid> UnitOfWork { get; }

    public IRepository<TEntity, TKey> Repository<TEntity, TKey>()
        where TEntity : BaseEntity<TKey>
        where TKey : notnull
        => new EfRepository<TEntity, TKey>(_dbContext);

    public void Dispose() => _dbContext.Dispose();

    private sealed class ConformanceUnitOfWork(DbContext dbContext, TimeProvider clock, ICurrentUserProvider<Guid>? currentUserProvider)
        : EfUnitOfWork<Guid>(dbContext, ConformanceDefaults.DefaultUser, serviceProvider: null, clock, currentUserProvider);
}

/// <summary>EF Core InMemory: one named in-memory database per store.</summary>
public sealed class EfInMemoryConformanceStore : IConformanceStore
{
    private readonly DbContextOptions<ConformanceDbContext> _options = new DbContextOptionsBuilder<ConformanceDbContext>()
        .UseInMemoryDatabase($"Conformance-{Guid.NewGuid()}")
        .Options;

    public IConformanceSession OpenSession(TimeProvider clock, ICurrentUserProvider<Guid>? currentUserProvider = null) =>
        new EfConformanceSession(new ConformanceDbContext(_options), clock, currentUserProvider);

    public void Dispose()
    {
        using var dbContext = new ConformanceDbContext(_options);
        dbContext.Database.EnsureDeleted();
    }
}

/// <summary>
/// EF Core SQLite in memory: a real relational provider, so SQL translation, generated keys and transactions are
/// exercised, which InMemory does not do. The database lives as long as the store's open connection.
/// </summary>
public sealed class EfSqliteConformanceStore : IConformanceStore
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly DbContextOptions<ConformanceDbContext> _options;

    public EfSqliteConformanceStore()
    {
        _connection.Open();
        _options = new DbContextOptionsBuilder<ConformanceDbContext>().UseSqlite(_connection).Options;

        using var dbContext = new ConformanceDbContext(_options);
        dbContext.Database.EnsureCreated();
    }

    public IConformanceSession OpenSession(TimeProvider clock, ICurrentUserProvider<Guid>? currentUserProvider = null) =>
        new EfConformanceSession(new ConformanceDbContext(_options), clock, currentUserProvider);

    public void Dispose() => _connection.Dispose();
}
