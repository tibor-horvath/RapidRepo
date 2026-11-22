using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RapidRepo.Auditing;
using RapidRepo.Entities;
using RapidRepo.Extensions;
using RapidRepo.Repositories.Interfaces;
using RapidRepo.Tenancy;
using RapidRepo.Testing.Conformance;
using RapidRepo.UnitOfWork;

namespace RapidRepo.EntityFrameworkCore.Tests.Conformance;

/// <summary>
/// The conformance entities mapped the way an application would map them: soft-deleted notes filtered out by
/// <see cref="ModelBuilderExtensions.AddSoftDeleteQueryFilters"/>, and tickets versioned by
/// <see cref="ModelConfigurationBuilderExtensions.AddConcurrencyTokens"/>.
/// </summary>
public sealed class ConformanceDbContext(DbContextOptions<ConformanceDbContext> options) : DbContext(options)
{
    public DbSet<Widget> Widgets => Set<Widget>();

    public DbSet<Note> Notes => Set<Note>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        configurationBuilder.AddConcurrencyTokens();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Widget>();
        modelBuilder.Entity<Note>();
        modelBuilder.Entity<Ticket>();
        modelBuilder.AddSoftDeleteQueryFilters();
    }
}

/// <summary>A session: one context, its unit of work, and repositories over that context.</summary>
public sealed class EfConformanceSession : IConformanceSession
{
    private readonly DbContext _dbContext;

    public EfConformanceSession(DbContext dbContext, TimeProvider clock, ICurrentUserProvider<Guid>? currentUserProvider)
    {
        _dbContext = dbContext;
        UnitOfWork = new ConformanceUnitOfWork(dbContext, clock, currentUserProvider);
    }

    public IUnitOfWork<Guid> UnitOfWork { get; }

    public IRepository<TEntity, TKey> Repository<TEntity, TKey>()
        where TEntity : BaseEntity<TKey>
        where TKey : notnull
        => new EfRepository<TEntity, TKey>(_dbContext);

    /// <summary>Creates a repository over this session's context that records calls to its write overrides.</summary>
    public IRepository<TEntity, TKey> CreateRecordingRepository<TEntity, TKey>(ICollection<string> calls)
        where TEntity : BaseEntity<TKey>
        where TKey : notnull
        => new RecordingRepository<TEntity, TKey>(_dbContext, calls);

    public void Dispose() => _dbContext.Dispose();

    private sealed class RecordingRepository<TEntity, TKey>(DbContext dbContext, ICollection<string> calls)
        : EfRepository<TEntity, TKey>(dbContext)
        where TEntity : BaseEntity<TKey>
        where TKey : notnull
    {
        public override void Delete(TEntity entity)
        {
            calls.Add(nameof(Delete));
            base.Delete(entity);
        }

        public override void HardDelete(TEntity entity)
        {
            calls.Add(nameof(HardDelete));
            base.HardDelete(entity);
        }

        public override void HardDeleteRange(IEnumerable<TEntity> entities)
        {
            calls.Add(nameof(HardDeleteRange));
            base.HardDeleteRange(entities);
        }

        public override void Restore(TEntity entity)
        {
            calls.Add(nameof(Restore));
            base.Restore(entity);
        }
    }

    private sealed class ConformanceUnitOfWork(DbContext dbContext, TimeProvider clock, ICurrentUserProvider<Guid>? currentUserProvider)
        : EfUnitOfWork<Guid>(dbContext, ConformanceDefaults.DefaultUser, serviceProvider: null, clock, currentUserProvider);
}

/// <summary>EF Core InMemory: one named in-memory database per store.</summary>
public sealed class EfInMemoryConformanceStore : IWriteRoutingConformanceStore
{
    private readonly DbContextOptions<ConformanceDbContext> _options = new DbContextOptionsBuilder<ConformanceDbContext>()
        .UseInMemoryDatabase($"Conformance-{Guid.NewGuid()}")
        .Options;

    public IConformanceSession OpenSession(TimeProvider clock, ICurrentUserProvider<Guid>? currentUserProvider = null) =>
        new EfConformanceSession(new ConformanceDbContext(_options), clock, currentUserProvider);

    public IRepository<TEntity, TKey> CreateRecordingRepository<TEntity, TKey>(IConformanceSession session, ICollection<string> calls)
        where TEntity : BaseEntity<TKey>
        where TKey : notnull
        => ((EfConformanceSession)session).CreateRecordingRepository<TEntity, TKey>(calls);

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
public sealed class EfSqliteConformanceStore : IWriteRoutingConformanceStore
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

    public IRepository<TEntity, TKey> CreateRecordingRepository<TEntity, TKey>(IConformanceSession session, ICollection<string> calls)
        where TEntity : BaseEntity<TKey>
        where TKey : notnull
        => ((EfConformanceSession)session).CreateRecordingRepository<TEntity, TKey>(calls);

    public void Dispose() => _connection.Dispose();
}

/// <summary>
/// The conformance entities plus the tenant entity, isolated the way an application would isolate them: the context
/// implements <see cref="ITenantDbContext{TTenantKey}"/> over a tenant provider, and
/// <see cref="ModelBuilderExtensions.AddTenantQueryFilters{TTenantKey}"/> filters every tenant entity.
/// </summary>
public sealed class TenantConformanceDbContext(
    DbContextOptions<TenantConformanceDbContext> options,
    ICurrentTenantProvider<Guid>? tenantProvider = null) : DbContext(options), ITenantDbContext<Guid>
{
    public Guid CurrentTenantId => tenantProvider?.GetCurrentTenantId() ?? Guid.Empty;

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        configurationBuilder.AddConcurrencyTokens();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Widget>();
        modelBuilder.Entity<Note>();
        modelBuilder.Entity<Ticket>();
        modelBuilder.Entity<Invoice>();
        modelBuilder.Entity<Payment>().HasOne<Invoice>().WithMany().HasForeignKey(p => p.InvoiceId);
        modelBuilder.AddSoftDeleteQueryFilters();
        modelBuilder.AddTenantQueryFilters(this);
    }
}

/// <summary>
/// The sessions of a tenancy store: without a tenant provider for the plain conformance tests, and with one for the
/// tenancy tests. Derived stores only choose the database.
/// </summary>
public abstract class EfTenantConformanceStore : ITenantConformanceStore
{
    protected abstract DbContextOptions<TenantConformanceDbContext> Options { get; }

    public IConformanceSession OpenSession(TimeProvider clock, ICurrentUserProvider<Guid>? currentUserProvider = null) =>
        new EfConformanceSession(new TenantConformanceDbContext(Options), clock, currentUserProvider);

    public IConformanceSession OpenSession(TimeProvider clock, ICurrentTenantProvider<Guid> tenantProvider) =>
        new EfConformanceSession(new TenantConformanceDbContext(Options, tenantProvider), clock, currentUserProvider: null);

    public abstract void Dispose();
}

/// <summary>EF Core InMemory, with tenancy.</summary>
public sealed class EfInMemoryTenantConformanceStore : EfTenantConformanceStore
{
    protected override DbContextOptions<TenantConformanceDbContext> Options { get; } = new DbContextOptionsBuilder<TenantConformanceDbContext>()
        .UseInMemoryDatabase($"TenantConformance-{Guid.NewGuid()}")
        .Options;

    public override void Dispose()
    {
        using var dbContext = new TenantConformanceDbContext(Options);
        dbContext.Database.EnsureDeleted();
    }
}

/// <summary>EF Core SQLite in memory, with tenancy.</summary>
public sealed class EfSqliteTenantConformanceStore : EfTenantConformanceStore
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public EfSqliteTenantConformanceStore()
    {
        _connection.Open();
        Options = new DbContextOptionsBuilder<TenantConformanceDbContext>().UseSqlite(_connection).Options;

        using var dbContext = new TenantConformanceDbContext(Options);
        dbContext.Database.EnsureCreated();
    }

    protected override DbContextOptions<TenantConformanceDbContext> Options { get; }

    public override void Dispose() => _connection.Dispose();
}
