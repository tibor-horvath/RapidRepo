using Microsoft.EntityFrameworkCore;
using RapidRepo.Entities;
using RapidRepo.Repositories;
using RapidRepo.Repositories.Interfaces;
using RapidRepo.UnitOfWork;

namespace RapidRepo.Extensions.DependencyInjection.Tests.TestData;

// ── Entities ──────────────────────────────────────────────────────────────────

public class Widget : BaseEntity<int> { }

public class Gadget : BaseEntity<Guid> { }

// ── DbContext ─────────────────────────────────────────────────────────────────

// A derived context, registered the way applications register one: AddDbContext<TestDbContext>().
public class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    public DbSet<Widget> Widgets => Set<Widget>();

    public DbSet<Gadget> Gadgets => Set<Gadget>();
}

// Two contexts owning disjoint entities — the multi-context scenario from the docs.
public class SalesDbContext(DbContextOptions<SalesDbContext> options) : DbContext(options)
{
    public DbSet<Widget> Widgets => Set<Widget>();
}

public class BillingDbContext(DbContextOptions<BillingDbContext> options) : DbContext(options)
{
    public DbSet<Gadget> Gadgets => Set<Gadget>();
}

// An entity mapped by two contexts — a shared lookup table, say.
public class SharedLookup : BaseEntity<int> { }

public class SharedLeftDbContext(DbContextOptions<SharedLeftDbContext> options) : DbContext(options)
{
    public DbSet<SharedLookup> Lookups => Set<SharedLookup>();
}

public class SharedRightDbContext(DbContextOptions<SharedRightDbContext> options) : DbContext(options)
{
    public DbSet<SharedLookup> Lookups => Set<SharedLookup>();
}

// Entity shapes that entity discovery has to sort through.
public class PremiumWidget : Widget { }

// Not a BaseEntity<> — EF maps it, but it cannot have a RapidRepo repository.
public class LegacyRecord
{
    public int Id { get; set; }
}

public class DiscoveryDbContext(DbContextOptions<DiscoveryDbContext> options) : DbContext(options)
{
    public DbSet<Widget> Widgets => Set<Widget>();

    // EF discovers DbSet properties regardless of accessibility, so discovery must too.
    internal DbSet<Gadget> Gadgets => Set<Gadget>();

    // Key type comes from a base class rather than the entity itself.
    public DbSet<PremiumWidget> PremiumWidgets => Set<PremiumWidget>();

    public DbSet<LegacyRecord> LegacyRecords => Set<LegacyRecord>();
}

// ── Interfaces ────────────────────────────────────────────────────────────────

public interface IWidgetRepository : IRepository<Widget, int> { }

public interface IGadgetRepository : IReadOnlyRepository<Gadget, Guid> { }

// ── Concrete repositories ─────────────────────────────────────────────────────

public class WidgetRepository(DbContext db) : BaseRepository<Widget, int>(db), IWidgetRepository { }

public class GadgetRepository(DbContext db) : ReadOnlyRepository<Gadget, Guid>(db), IGadgetRepository { }

// Self-only (no user-defined interface) — must not be registered against any interface
public class NoInterfaceRepository(DbContext db) : BaseRepository<Widget, int>(db) { }

// Repositories naming their own derived context — resolvable without a DbContext forwarder,
// and the only way to disambiguate when several contexts are in play.
public interface ISalesWidgetRepository : IRepository<Widget, int> { }

public class SalesWidgetRepository(SalesDbContext db) : BaseRepository<Widget, int>(db), ISalesWidgetRepository { }

public interface IBillingGadgetRepository : IRepository<Gadget, Guid> { }

public class BillingGadgetRepository(BillingDbContext db) : BaseRepository<Gadget, Guid>(db), IBillingGadgetRepository { }

// Abstract — must be skipped
public abstract class AbstractRepository(DbContext db) : BaseRepository<Widget, int>(db), IWidgetRepository { }

// Open generic — must be skipped
public class OpenRepository<TEntity, TKey>(DbContext db)
    : BaseRepository<TEntity, TKey>(db), IRepository<TEntity, TKey>
    where TEntity : BaseEntity<TKey>
    where TKey : notnull
{ }

// ── Unit of Work ──────────────────────────────────────────────────────────────

public interface ITestUnitOfWork : IUnitOfWork<Guid> { }

public class TestUnitOfWork(DbContext db) : UnitOfWork<Guid>(db, Guid.Empty), ITestUnitOfWork;

public interface ISecondUnitOfWork : IUnitOfWork<int> { }

public class SecondUnitOfWork(DbContext db) : UnitOfWork<int>(db, 0), ISecondUnitOfWork;

// UnitOfWork with no user-defined interface — used to verify UseUnitOfWork<TImpl>() error path.
public class DirectUnitOfWork(DbContext db) : UnitOfWork<Guid>(db, Guid.Empty);

// UnitOfWork implementing two user-defined interfaces — used to verify ambiguity error path.
public interface IFirstUoW : IUnitOfWork<Guid> { }
public interface ISecondUoW : IUnitOfWork<Guid> { }
public class MultiInterfaceUnitOfWork(DbContext db) : UnitOfWork<Guid>(db, Guid.Empty), IFirstUoW, ISecondUoW;

// Abstract UoW — used to verify UseUnitOfWork<TImpl>() abstract guard.
public interface IAbstractUoW : IUnitOfWork<Guid> { }
public abstract class AbstractUoW(DbContext db) : UnitOfWork<Guid>(db, Guid.Empty), IAbstractUoW;

// Third UoW sharing the same TUserKey (Guid) as TestUnitOfWork — used to verify forwarding alias first-one-wins.
public interface IThirdUoW : IUnitOfWork<Guid> { }
public class ThirdUnitOfWork(DbContext db) : UnitOfWork<Guid>(db, Guid.Empty), IThirdUoW;
