using RapidRepo.Entities;
using RapidRepo.Repositories.Interfaces;
using RapidRepo.UnitOfWork;

namespace RapidRepo.Extensions.DependencyInjection.Tests.TestData;

// Provider-neutral test data: repositories and units of work implement the core contracts directly, so this suite
// exercises AddRapidRepo without any persistence provider. EF-specific registration is tested in
// RapidRepo.EntityFrameworkCore.Tests.

// ── Entities ──────────────────────────────────────────────────────────────────

public class Widget : BaseEntity<int> { }

public class Gadget : BaseEntity<Guid> { }

// ── Interfaces ────────────────────────────────────────────────────────────────

public interface IWidgetRepository : IRepository<Widget, int> { }

public interface IGadgetRepository : IReadOnlyRepository<Gadget, Guid> { }

// ── Concrete repositories ─────────────────────────────────────────────────────

public class WidgetRepository : StubRepository<Widget, int>, IWidgetRepository { }

public class GadgetRepository : StubRepository<Gadget, Guid>, IGadgetRepository { }

// Self-only (no user-defined interface) — must not be registered against any interface
public class NoInterfaceRepository : StubRepository<Widget, int> { }

// A second module's repository for the same entity, behind its own interface.
public interface ISalesWidgetRepository : IRepository<Widget, int> { }

public class SalesWidgetRepository : StubRepository<Widget, int>, ISalesWidgetRepository { }

public interface IBillingGadgetRepository : IRepository<Gadget, Guid> { }

public class BillingGadgetRepository : StubRepository<Gadget, Guid>, IBillingGadgetRepository { }

// Abstract — must be skipped
public abstract class AbstractRepository : StubRepository<Widget, int>, IWidgetRepository { }

// Open generic — must be skipped
public class OpenRepository<TEntity, TKey> : StubRepository<TEntity, TKey>, IRepository<TEntity, TKey>
    where TEntity : BaseEntity<TKey>
    where TKey : notnull
{ }

// ── Unit of Work ──────────────────────────────────────────────────────────────

public abstract class StubUnitOfWork<TUserKey> : IUnitOfWork<TUserKey>
    where TUserKey : struct
{
    public void Commit(TUserKey? userId = null) => throw new NotSupportedException();

    public Task CommitAsync(TUserKey? userId = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}

public interface ITestUnitOfWork : IUnitOfWork<Guid> { }

public class TestUnitOfWork : StubUnitOfWork<Guid>, ITestUnitOfWork;

public interface ISecondUnitOfWork : IUnitOfWork<int> { }

public class SecondUnitOfWork : StubUnitOfWork<int>, ISecondUnitOfWork;

// UnitOfWork with no user-defined interface — used to verify UseUnitOfWork<TImpl>() error path.
public class DirectUnitOfWork : StubUnitOfWork<Guid>;

// UnitOfWork implementing two user-defined interfaces — used to verify ambiguity error path.
public interface IFirstUoW : IUnitOfWork<Guid> { }
public interface ISecondUoW : IUnitOfWork<Guid> { }
public class MultiInterfaceUnitOfWork : StubUnitOfWork<Guid>, IFirstUoW, ISecondUoW;

// Abstract UoW — used to verify UseUnitOfWork<TImpl>() abstract guard.
public interface IAbstractUoW : IUnitOfWork<Guid> { }
public abstract class AbstractUoW : StubUnitOfWork<Guid>, IAbstractUoW;

// Third UoW sharing the same TUserKey (Guid) as TestUnitOfWork — used to verify forwarding alias first-one-wins.
public interface IThirdUoW : IUnitOfWork<Guid> { }
public class ThirdUnitOfWork : StubUnitOfWork<Guid>, IThirdUoW;
