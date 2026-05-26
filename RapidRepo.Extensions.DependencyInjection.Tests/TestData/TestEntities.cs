using Microsoft.EntityFrameworkCore;
using RapidRepo.Entities;
using RapidRepo.Repositories;
using RapidRepo.Repositories.Interfaces;
using RapidRepo.UnitOfWork;

namespace RapidRepo.Extensions.DependencyInjection.Tests.TestData;

// ── Entities ──────────────────────────────────────────────────────────────────

public class Widget : BaseEntity<int> { }

public class Gadget : BaseEntity<Guid> { }

// ── Interfaces ────────────────────────────────────────────────────────────────

public interface IWidgetRepository : IRepository<Widget, int> { }

public interface IGadgetRepository : IReadOnlyRepository<Gadget, Guid> { }

// ── Concrete repositories ─────────────────────────────────────────────────────

public class WidgetRepository(DbContext db) : BaseRepository<Widget, int>(db), IWidgetRepository { }

public class GadgetRepository(DbContext db) : ReadOnlyRepository<Gadget, Guid>(db), IGadgetRepository { }

// Self-only (no user-defined interface) — must not be registered against any interface
public class NoInterfaceRepository(DbContext db) : BaseRepository<Widget, int>(db) { }

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

public class TestUnitOfWork(DbContext db) : UnitOfWork<Guid>(db), ITestUnitOfWork
{
    public override Guid DefaultUserKey => Guid.Empty;
}

public interface ISecondUnitOfWork : IUnitOfWork<int> { }

public class SecondUnitOfWork(DbContext db) : UnitOfWork<int>(db), ISecondUnitOfWork
{
    public override int DefaultUserKey => 0;
}
