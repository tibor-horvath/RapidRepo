using AwesomeAssertions;
using RapidRepo.Auditing;
using RapidRepo.Entities;
using RapidRepo.Repositories.Interfaces;
using RapidRepo.UnitOfWork;

namespace RapidRepo.Tests.UnitOfWork;

/// <summary>
/// The audit user a commit hands to the provider: the explicit <c>userId</c>, else the
/// <see cref="ICurrentUserProvider{TUserKey}"/>, else <see langword="null"/> (the default user key), or a throw in
/// strict mode.
/// </summary>
public class CurrentUserResolutionTests
{
    private static readonly Guid Explicit = new("00000000-0000-0000-0000-0000000000e1");
    private static readonly Guid DefaultUser = new("00000000-0000-0000-0000-0000000000d0");
    private static readonly Guid Ambient = new("00000000-0000-0000-0000-0000000000a1");

    [Fact]
    public void WithoutAProvider_TheCommitsUserIsPassedThroughUnchanged()
    {
        var uow = new RecordingUnitOfWork();

        uow.Commit(Explicit);
        uow.Commit();

        uow.Users.Should().Equal(Explicit, null);
    }

    [Fact]
    public async Task ProviderUser_IsUsed_WhenTheCommitNamesNone()
    {
        var provider = new CountingProvider(Ambient);
        var uow = new RecordingUnitOfWork(currentUserProvider: provider);

        uow.Commit();
        await uow.CommitAsync();

        uow.Users.Should().Equal(Ambient, Ambient);
    }

    [Fact]
    public async Task ExplicitUser_OverridesTheProvider_WithoutAskingIt()
    {
        var provider = new CountingProvider(Ambient);
        var uow = new RecordingUnitOfWork(currentUserProvider: provider);

        uow.Commit(Explicit);
        await uow.CommitAsync(Explicit);

        uow.Users.Should().Equal(Explicit, Explicit);
        provider.Calls.Should().Be(0);
    }

    [Fact]
    public void Provider_IsAskedOncePerCommit()
    {
        var provider = new CountingProvider(Ambient);
        var uow = new RecordingUnitOfWork(currentUserProvider: provider);

        uow.Commit();
        uow.Commit();

        provider.Calls.Should().Be(2);
    }

    [Fact]
    public void ProviderReturningNull_FallsBackToTheDefaultUser()
    {
        var uow = new RecordingUnitOfWork(currentUserProvider: new CountingProvider(null));

        uow.Commit();

        uow.Users.Should().Equal([null], "null makes the provider stamp the default user key");
    }

    [Fact]
    public void Provider_IsResolvedFromTheServiceProvider_WhenNotPassed()
    {
        var services = new DictionaryServiceProvider
        {
            [typeof(ICurrentUserProvider<Guid>)] = new CountingProvider(Ambient),
        };
        var uow = new RecordingUnitOfWork(services);

        uow.Commit();

        uow.Users.Should().Equal(Ambient);
    }

    [Fact]
    public void PassedProvider_WinsOverTheServiceProvidersOne()
    {
        var services = new DictionaryServiceProvider
        {
            [typeof(ICurrentUserProvider<Guid>)] = new CountingProvider(Guid.NewGuid()),
        };
        var uow = new RecordingUnitOfWork(services, new CountingProvider(Ambient));

        uow.Commit();

        uow.Users.Should().Equal(Ambient);
    }

    [Fact]
    public void Provider_IsResolvedAtTheFirstCommit_NotInTheConstructor()
    {
        var services = new DictionaryServiceProvider();
        var uow = new RecordingUnitOfWork(services);
        services[typeof(ICurrentUserProvider<Guid>)] = new CountingProvider(Ambient);

        uow.Commit();

        uow.Users.Should().Equal(Ambient);
    }

    [Fact]
    public async Task StrictMode_WithoutAnyUser_Throws_WhenAUserFieldIsStamped()
    {
        var uow = new RecordingUnitOfWork(currentUserProvider: new CountingProvider(null), requireCurrentUser: true);
        uow.Staged.Add(new AuditedByUser());

        var commit = () => uow.Commit();
        var commitAsync = () => uow.CommitAsync();

        commit.Should().Throw<InvalidOperationException>().WithMessage("*returned null*RequireCurrentUser*");
        await commitAsync.Should().ThrowAsync<InvalidOperationException>();
        uow.Users.Should().BeEmpty("the throw comes before anything is persisted");
    }

    [Fact]
    public void StrictMode_WithoutAProvider_SaysNoneIsConfigured()
    {
        var uow = new RecordingUnitOfWork(requireCurrentUser: true);
        uow.Staged.Add(new AuditedByUser());

        var commit = () => uow.Commit();

        commit.Should().Throw<InvalidOperationException>().WithMessage("*no ICurrentUserProvider<Guid> is configured*");
    }

    [Fact]
    public void StrictMode_DoesNotNeedAUser_WhenNoUserFieldIsStamped()
    {
        var uow = new RecordingUnitOfWork(requireCurrentUser: true);
        var timeOnly = new AuditedWithoutUser();
        uow.Staged.Add(new Plain());
        uow.Staged.Add(timeOnly);

        uow.Commit();
        uow.Staged.Clear();
        uow.Commit();

        uow.Users.Should().Equal(new Guid?[] { null, null });
        timeOnly.CreatedAt.Should().NotBe(default, "the timestamp is still stamped");
    }

    [Fact]
    public void StrictMode_IsSatisfiedByTheProvider_OrAnExplicitUser()
    {
        var uow = new RecordingUnitOfWork(currentUserProvider: new CountingProvider(Ambient), requireCurrentUser: true);
        var withoutProvider = new RecordingUnitOfWork(requireCurrentUser: true);
        var first = new AuditedByUser();
        var second = new AuditedByUser();
        uow.Staged.Add(first);
        withoutProvider.Staged.Add(second);

        uow.Commit();
        withoutProvider.Commit(Explicit);

        first.CreatedBy.Should().Be(Ambient);
        second.CreatedBy.Should().Be(Explicit);
    }

    [Fact]
    public void WithoutStrictMode_AMissingUser_StampsTheDefaultUser()
    {
        var uow = new RecordingUnitOfWork(currentUserProvider: new CountingProvider(null));
        var entity = new AuditedByUser();
        uow.Staged.Add(entity);

        uow.Commit();

        entity.CreatedBy.Should().Be(DefaultUser);
    }

    [Fact]
    public void StrictMode_IsReadFromCurrentUserOptions_InTheServiceProvider()
    {
        var services = new DictionaryServiceProvider
        {
            [typeof(CurrentUserOptions)] = new CurrentUserOptions { RequireCurrentUser = true },
        };
        var uow = new RecordingUnitOfWork(services);
        uow.Staged.Add(new AuditedByUser());

        var commit = () => uow.Commit();

        commit.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void StrictMode_IsReadAtTheFirstCommit_NotInTheConstructor()
    {
        var services = new DictionaryServiceProvider();
        var uow = new RecordingUnitOfWork(services);
        services[typeof(CurrentUserOptions)] = new CurrentUserOptions { RequireCurrentUser = true };
        uow.Staged.Add(new AuditedByUser());

        var commit = () => uow.Commit();

        commit.Should().Throw<InvalidOperationException>();
    }

    public sealed class Plain : BaseEntity<int>;

    public sealed class AuditedWithoutUser : BaseAuditableEntity<int>;

    public sealed class AuditedByUser : BaseAuditableEntity<int, Guid>;

    private sealed class RecordingUnitOfWork(
        IServiceProvider? serviceProvider = null,
        ICurrentUserProvider<Guid>? currentUserProvider = null,
        bool? requireCurrentUser = null)
        : UnitOfWorkBase<Guid>(DefaultUser, serviceProvider, timeProvider: null, currentUserProvider)
    {
        /// <summary>The users of the commits that went through.</summary>
        public List<Guid?> Users { get; } = [];

        /// <summary>Entities each commit stamps as added before "persisting".</summary>
        public List<object> Staged { get; } = [];

        protected override bool RequireCurrentUser => requireCurrentUser ?? base.RequireCurrentUser;

        protected override IRepository<TEntity, TKey> CreateRepository<TEntity, TKey>() =>
            throw new NotSupportedException();

        protected override void CommitCore(Guid? userId)
        {
            foreach (var entity in Staged)
                Auditor.Stamp(entity, EntityChangeKind.Added, userId);

            Users.Add(userId);
        }

        protected override Task CommitCoreAsync(Guid? userId, CancellationToken cancellationToken)
        {
            CommitCore(userId);
            return Task.CompletedTask;
        }
    }

    private sealed class CountingProvider(Guid? user) : ICurrentUserProvider<Guid>
    {
        public int Calls { get; private set; }

        public Guid? GetCurrentUserId()
        {
            Calls++;
            return user;
        }
    }

    private sealed class DictionaryServiceProvider : Dictionary<Type, object>, IServiceProvider
    {
        public object? GetService(Type serviceType) => TryGetValue(serviceType, out var service) ? service : null;
    }
}
