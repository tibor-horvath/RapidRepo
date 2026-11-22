using AwesomeAssertions;
using RapidRepo.Entities;
using RapidRepo.Repositories.Interfaces;
using RapidRepo.UnitOfWork;

namespace RapidRepo.Tests.UnitOfWork;

/// <summary>
/// The provider-neutral part of a unit of work: commit entry points, the repository accessors generated code
/// calls, and the auditor's clock.
/// </summary>
public class UnitOfWorkBaseTests
{
    private static readonly Guid DefaultUser = Guid.NewGuid();

    public sealed class Item : BaseEntity<int>;

    public interface IItemRepository;

    private sealed class ItemRepository : IItemRepository;

    [Fact]
    public void Commit_PassesTheUserToTheProvider()
    {
        var uow = new FakeUnitOfWork();
        var user = Guid.NewGuid();

        uow.Commit(user);
        uow.Commit();

        uow.Commits.Should().Equal((user, false), (null, false));
    }

    [Fact]
    public async Task CommitAsync_PassesTheUserAndTheCancellationToken()
    {
        var uow = new FakeUnitOfWork();
        using var cts = new CancellationTokenSource();
        var user = Guid.NewGuid();

        await uow.CommitAsync(user, cts.Token);

        uow.Commits.Should().Equal((user, true));
        uow.LastToken.Should().Be(cts.Token);
    }

    [Fact]
    public void GetRepository_ReturnsTheProvidersRepository()
    {
        var uow = new FakeUnitOfWork();

        uow.Items.Should().BeSameAs(FakeUnitOfWork.Repository);
    }

    [Fact]
    public void ResolveRepository_WithoutAServiceProvider_Throws()
    {
        var uow = new FakeUnitOfWork();

        var act = () => uow.Resolve<IItemRepository>();

        act.Should().Throw<InvalidOperationException>().WithMessage("*requires an IServiceProvider*");
    }

    [Fact]
    public void ResolveRepository_ForAnUnregisteredService_Throws()
    {
        var uow = new FakeUnitOfWork(new DictionaryServiceProvider());

        var act = () => uow.Resolve<IItemRepository>();

        act.Should().Throw<InvalidOperationException>().WithMessage("*'IItemRepository' is registered*");
    }

    [Fact]
    public void ResolveRepository_ReturnsTheRegisteredService()
    {
        var repository = new ItemRepository();
        var uow = new FakeUnitOfWork(new DictionaryServiceProvider { [typeof(IItemRepository)] = repository });

        uow.Resolve<IItemRepository>().Should().BeSameAs(repository);
    }

    [Fact]
    public void Auditor_UsesTheGivenClock_AndDefaultUser()
    {
        var now = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
        var uow = new FakeUnitOfWork(timeProvider: new FixedClock(now));

        uow.AuditorNow.Should().Be(now);
        uow.AuditorDefaultUser.Should().Be(DefaultUser);
    }

    private sealed class FakeUnitOfWork(IServiceProvider? serviceProvider = null, TimeProvider? timeProvider = null)
        : UnitOfWorkBase<Guid>(DefaultUser, serviceProvider, timeProvider)
    {
        public static readonly IRepository<Item, int> Repository = UnusableRepository.Create();

        public List<(Guid? User, bool Async)> Commits { get; } = [];

        public CancellationToken LastToken { get; private set; }

        public IRepository<Item, int> Items => GetRepository<Item, int>();

        public DateTime AuditorNow => Auditor.UtcNow;

        public Guid AuditorDefaultUser => Auditor.DefaultUserKey;

        public TRepo Resolve<TRepo>() where TRepo : class => ResolveRepository<TRepo>();

        protected override IRepository<TEntity, TKey> CreateRepository<TEntity, TKey>() =>
            (IRepository<TEntity, TKey>)Repository;

        protected override void CommitCore(Guid? userId) => Commits.Add((userId, false));

        protected override Task CommitCoreAsync(Guid? userId, CancellationToken cancellationToken)
        {
            Commits.Add((userId, true));
            LastToken = cancellationToken;
            return Task.CompletedTask;
        }
    }

    /// <summary>A stand-in repository; only its identity matters here, so every call throws.</summary>
    public class UnusableRepository : System.Reflection.DispatchProxy
    {
        internal static IRepository<Item, int> Create() => Create<IRepository<Item, int>, UnusableRepository>();

        protected override object? Invoke(System.Reflection.MethodInfo? targetMethod, object?[]? args) =>
            throw new NotSupportedException();
    }

    private sealed class DictionaryServiceProvider : Dictionary<Type, object>, IServiceProvider
    {
        public object? GetService(Type serviceType) => TryGetValue(serviceType, out var service) ? service : null;
    }

    private sealed class FixedClock(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}
