using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RapidRepo.Entities;
using RapidRepo.Entities.Interfaces;
using RapidRepo.Extensions;
using RapidRepo.Tenancy;

namespace RapidRepo.EntityFrameworkCore.Tests.Tenancy;

public class Order : BaseEntity<int>, ITenantEntity<Guid>, IDeletableEntity
{
    public Guid TenantId { get; set; }

    public string Title { get; set; } = "";

    public DateTime? DeletedAt { get; set; }

    public List<OrderLine> Lines { get; set; } = [];
}

public class OrderLine : BaseEntity<int>, ITenantEntity<Guid>
{
    public Guid TenantId { get; set; }

    public int OrderId { get; set; }

    public string Text { get; set; } = "";
}

/// <summary>Not a tenant entity, but refers to one: a link row the commit still keeps within the current tenant.</summary>
public class OrderNote : BaseEntity<int>
{
    public int OrderId { get; set; }

    /// <summary>A navigation into tenant data from an entity that is not tenant-filtered itself.</summary>
    public Order? Order { get; set; }

    public string Text { get; set; } = "";
}

/// <summary>
/// Not a tenant entity, with a composite foreign key that includes TenantId: the database keeps it within the tenant
/// the caller writes into <see cref="TenantId"/>, which nothing checks, so the commit must still check the reference.
/// </summary>
public class OrderTag : BaseEntity<int>
{
    public Guid TenantId { get; set; }

    public int OrderLineId { get; set; }
}

/// <summary>A non-tenant, soft-deletable entity with an anonymous filter: in a model with tenant data, lifting it must throw.</summary>
public class Product : BaseEntity<int>, IDeletableEntity
{
    public string Name { get; set; } = "";

    public DateTime? DeletedAt { get; set; }
}

/// <summary>A tenant entity with a composite primary key.</summary>
public class Membership : ITenantEntity<Guid>
{
    public int OrgId { get; set; }

    public int UserId { get; set; }

    public Guid TenantId { get; set; }

    public string Role { get; set; } = "";
}

/// <summary>A mutable tenant, standing in for the request.</summary>
public sealed class TestTenant : ICurrentTenantProvider<Guid>
{
    public Guid Current { get; set; }

    public Guid GetCurrentTenantId() => Current;
}

/// <summary>A provider with no request to read, as in a background job: asking it is a bug.</summary>
public sealed class ThrowingTenant : ICurrentTenantProvider<Guid>
{
    public Guid GetCurrentTenantId() => throw new InvalidOperationException("The tenant provider was asked inside an override.");
}

/// <summary>The documented setup: the context delegates <see cref="ITenantDbContext{TTenantKey}.CurrentTenantId"/> to a provider.</summary>
public class OrderContext(DbContextOptions options, ICurrentTenantProvider<Guid>? tenants = null) : DbContext(options), ITenantDbContext<Guid>
{
    public Guid CurrentTenantId => tenants?.GetCurrentTenantId() ?? Guid.Empty;

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderLine> OrderLines => Set<OrderLine>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<Membership> Memberships => Set<Membership>();

    public DbSet<OrderNote> OrderNotes => Set<OrderNote>();

    public DbSet<OrderTag> OrderTags => Set<OrderTag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>().HasMany(o => o.Lines).WithOne().HasForeignKey(l => l.OrderId);
        modelBuilder.Entity<OrderNote>().HasOne(n => n.Order).WithMany().HasForeignKey(n => n.OrderId);
        modelBuilder.Entity<OrderTag>().HasOne<OrderLine>().WithMany()
            .HasForeignKey(t => new { t.TenantId, t.OrderLineId })
            .HasPrincipalKey(l => new { l.TenantId, l.Id });
        modelBuilder.Entity<Product>().HasQueryFilter(p => p.Name != "hidden");
        modelBuilder.Entity<Membership>().HasKey(m => new { m.OrgId, m.UserId });
        modelBuilder.AddSoftDeleteQueryFilters();
        ConfigureTenancy(modelBuilder);
    }

    protected virtual void ConfigureTenancy(ModelBuilder modelBuilder) => modelBuilder.AddTenantQueryFilters(this);
}

/// <summary>Opts into failing on foreign keys that don't include TenantId.</summary>
public sealed class StrictForeignKeyContext(DbContextOptions options) : OrderContext(options)
{
    protected override void ConfigureTenancy(ModelBuilder modelBuilder) =>
        modelBuilder.AddTenantQueryFilters(this, o => o.RequireTenantScopedForeignKeys = true);
}

/// <summary>Replaces the tenant filter of <see cref="Order"/> with one that matches every row.</summary>
public sealed class ReplacedFilterContext(DbContextOptions options) : OrderContext(options)
{
    protected override void ConfigureTenancy(ModelBuilder modelBuilder)
    {
        base.ConfigureTenancy(modelBuilder);
        modelBuilder.Entity<Order>().HasQueryFilter(ModelBuilderExtensions.TenantFilterKey, o => true);
    }
}

/// <summary>Forgets AddTenantQueryFilters.</summary>
public sealed class MissingFilterContext(DbContextOptions options) : OrderContext(options)
{
    protected override void ConfigureTenancy(ModelBuilder modelBuilder)
    {
    }
}

/// <summary>Maps a tenant entity but cannot supply the tenant.</summary>
public sealed class NoTenantSourceContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();
}

/// <summary>Keeps a hand-written anonymous tenant filter.</summary>
public sealed class HandWrittenFilterContext(DbContextOptions options) : DbContext(options), ITenantDbContext<Guid>
{
    public Guid CurrentTenantId => Guid.Empty;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrderLine>().HasQueryFilter(l => l.TenantId == CurrentTenantId);
        modelBuilder.AddTenantQueryFilters(this);
    }
}

/// <summary>No tenant entities at all.</summary>
public sealed class PlainContext(DbContextOptions<PlainContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
}

public class Document : BaseEntity<int>, ITenantEntity<string>
{
    public string TenantId { get; set; } = null!;

    public string Title { get; set; } = "";
}

/// <summary>String tenant keys.</summary>
public sealed class DocumentContext(DbContextOptions options, Func<string?> tenant) : DbContext(options), ITenantDbContext<string>
{
    public string? CurrentTenantId => tenant();

    public DbSet<Document> Documents => Set<Document>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.AddTenantQueryFilters(this);
}

public sealed class TestUnitOfWork(DbContext dbContext) : EfUnitOfWork<Guid>(dbContext, Guid.Empty);

/// <summary>An open in-memory SQLite database, shared by the contexts created over it.</summary>
/// <summary>A tenant entity with a string key that the database compares case-insensitively.</summary>
public class Sku : BaseEntity<string>, ITenantEntity<Guid>
{
    public Guid TenantId { get; set; }

    public string Name { get; set; } = "";
}

/// <summary>Refers to a <see cref="Sku"/> by its case-insensitive code.</summary>
public class StockLine : BaseEntity<int>, ITenantEntity<Guid>
{
    public Guid TenantId { get; set; }

    public string SkuCode { get; set; } = "";
}

/// <summary>Keys compared with SQLite's NOCASE collation, as SQL Server's default collation would.</summary>
public sealed class SkuContext(DbContextOptions options, ICurrentTenantProvider<Guid> tenants) : DbContext(options), ITenantDbContext<Guid>
{
    public Guid CurrentTenantId => tenants.GetCurrentTenantId();

    public DbSet<Sku> Skus => Set<Sku>();

    public DbSet<StockLine> StockLines => Set<StockLine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Sku>().Property(s => s.Id).UseCollation("NOCASE");
        modelBuilder.Entity<StockLine>().Property(l => l.SkuCode).UseCollation("NOCASE");
        modelBuilder.Entity<StockLine>().HasOne<Sku>().WithMany().HasForeignKey(l => l.SkuCode);
        modelBuilder.AddTenantQueryFilters(this);
    }
}

public sealed class SqliteDatabase : IDisposable
{
    public SqliteDatabase() => Connection.Open();

    public SqliteConnection Connection { get; } = new("DataSource=:memory:");

    public DbContextOptions Options => new DbContextOptionsBuilder().UseSqlite(Connection).Options;

    public void Dispose() => Connection.Dispose();
}
