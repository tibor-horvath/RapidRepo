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
