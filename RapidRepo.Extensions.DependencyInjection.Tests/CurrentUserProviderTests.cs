using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RapidRepo.Auditing;
using RapidRepo.Entities;
using RapidRepo.Repositories.Interfaces;
using RapidRepo.UnitOfWork;

namespace RapidRepo.Extensions.DependencyInjection.Tests;

/// <summary>
/// <see cref="RapidRepoOptions.UseCurrentUserProvider{TProvider,TUserKey}"/>,
/// <see cref="RapidRepoOptions.UseCurrentUser{TUserKey}"/> and <see cref="RapidRepoOptions.RequireCurrentUser"/>.
/// </summary>
public class CurrentUserProviderTests
{
    private static readonly Guid RequestUser = new("00000000-0000-0000-0000-0000000000a1");

    // ── Registration ───────────────────────────────────────────────────────────

    [Fact]
    public void UseCurrentUserProvider_RegistersTheProvider_WithTheUnitOfWorkLifetime()
    {
        var services = new ServiceCollection();

        services.AddRapidRepo(o =>
        {
            o.UseCurrentUserProvider<RequestUserProvider, Guid>();
            o.Lifetime = ServiceLifetime.Transient; // set after the call: the final lifetime still applies
        });

        services.Should().ContainSingle(d => d.ServiceType == typeof(ICurrentUserProvider<Guid>))
            .Which.Should().Match<ServiceDescriptor>(d =>
                d.ImplementationType == typeof(RequestUserProvider) && d.Lifetime == ServiceLifetime.Transient);
    }

    [Fact]
    public void UseCurrentUserProvider_DefaultsToScoped()
    {
        var services = new ServiceCollection();

        services.AddRapidRepo(o => o.UseCurrentUserProvider<RequestUserProvider, Guid>());

        services.Single(d => d.ServiceType == typeof(ICurrentUserProvider<Guid>)).Lifetime
            .Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void UseCurrentUser_ResolvesThroughTheDelegate_WithTheScopesServices()
    {
        var services = new ServiceCollection();
        services.AddScoped<RequestContext>();
        services.AddRapidRepo(o => o.UseCurrentUser<Guid>(sp => sp.GetRequiredService<RequestContext>().UserId));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<RequestContext>().UserId = RequestUser;

        scope.ServiceProvider.GetRequiredService<ICurrentUserProvider<Guid>>().GetCurrentUserId()
            .Should().Be(RequestUser);
    }

    [Fact]
    public void UseCurrentUser_NullDelegate_Throws()
    {
        var act = () => new RapidRepoOptions().UseCurrentUser<Guid>(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SecondProvider_ForTheSameUserKey_Throws()
    {
        var options = new RapidRepoOptions().UseCurrentUserProvider<RequestUserProvider, Guid>();

        var act = () => options.UseCurrentUser<Guid>(_ => null);

        act.Should().Throw<InvalidOperationException>().WithMessage("*already been configured*'Guid'*");
    }

    [Fact]
    public void Providers_ForDifferentUserKeys_AreBothRegistered()
    {
        var services = new ServiceCollection();

        services.AddRapidRepo(o => o
            .UseCurrentUserProvider<RequestUserProvider, Guid>()
            .UseCurrentUser<int>(_ => 42));

        services.Should().Contain(d => d.ServiceType == typeof(ICurrentUserProvider<Guid>));
        services.Should().Contain(d => d.ServiceType == typeof(ICurrentUserProvider<int>));
    }

    [Fact]
    public void AbstractProvider_Throws()
    {
        var act = () => new RapidRepoOptions().UseCurrentUserProvider<AbstractUserProvider, Guid>();

        act.Should().Throw<ArgumentException>().WithMessage("*abstract*");
    }

    [Fact]
    public void ApplicationRegistration_TakesPrecedence()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserProvider<Guid>, FixedUserProvider>();

        services.AddRapidRepo(o => o.UseCurrentUserProvider<RequestUserProvider, Guid>());

        services.Should().ContainSingle(d => d.ServiceType == typeof(ICurrentUserProvider<Guid>))
            .Which.ImplementationType.Should().Be<FixedUserProvider>();
    }

    [Fact]
    public void RequireCurrentUser_RegistersCurrentUserOptions()
    {
        var services = new ServiceCollection();

        services.AddRapidRepo(o => o.RequireCurrentUser = true);

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<CurrentUserOptions>().RequireCurrentUser.Should().BeTrue();
    }

    [Fact]
    public void RequireCurrentUser_Off_RegistersNothing()
    {
        var services = new ServiceCollection();

        services.AddRapidRepo(_ => { });

        services.Should().NotContain(d => d.ServiceType == typeof(CurrentUserOptions));
    }

    [Fact]
    public void SingletonLifetime_WithACurrentUserProvider_MentionsItInTheMisuseError()
    {
        var services = new ServiceCollection();

        var act = () => services.AddRapidRepo(o =>
        {
            o.Lifetime = ServiceLifetime.Singleton;
            o.ThrowOnSingletonMisuse = true;
            o.UseCurrentUserProvider<RequestUserProvider, Guid>();
        });

        act.Should().Throw<InvalidOperationException>().WithMessage("*current-user provider is registered as Singleton*");
    }

    // ── End to end: a unit of work resolved from the container ─────────────────

    [Fact]
    public void UnitOfWork_StampsTheProvidersUser_WhenTheCommitNamesNone()
    {
        using var provider = BuildProvider(o => o.UseCurrentUser<Guid>(sp => sp.GetRequiredService<RequestContext>().UserId));
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<RequestContext>().UserId = RequestUser;
        var uow = scope.ServiceProvider.GetRequiredService<IAppUnitOfWork>();

        uow.Commit();
        uow.Commit(Guid.Empty);

        ((AppUnitOfWork)uow).Users.Should().Equal(RequestUser, Guid.Empty);
    }

    [Fact]
    public void UnitOfWork_EachScopeSeesItsOwnUser()
    {
        using var provider = BuildProvider(o => o.UseCurrentUserProvider<RequestUserProvider, Guid>());
        var other = Guid.NewGuid();

        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        first.ServiceProvider.GetRequiredService<RequestContext>().UserId = RequestUser;
        second.ServiceProvider.GetRequiredService<RequestContext>().UserId = other;

        first.ServiceProvider.GetRequiredService<IAppUnitOfWork>().Commit();
        second.ServiceProvider.GetRequiredService<IAppUnitOfWork>().Commit();

        ((AppUnitOfWork)first.ServiceProvider.GetRequiredService<IAppUnitOfWork>()).Users.Should().Equal(RequestUser);
        ((AppUnitOfWork)second.ServiceProvider.GetRequiredService<IAppUnitOfWork>()).Users.Should().Equal(other);
    }

    [Fact]
    public void UnitOfWork_InStrictMode_ThrowsWhenThereIsNoUser()
    {
        using var provider = BuildProvider(o =>
        {
            o.UseCurrentUserProvider<RequestUserProvider, Guid>();
            o.RequireCurrentUser = true;
        });
        using var scope = provider.CreateScope();
        var uow = (AppUnitOfWork)scope.ServiceProvider.GetRequiredService<IAppUnitOfWork>();
        uow.Staged.Add(new AuditedByUser());

        var act = () => uow.Commit();

        act.Should().Throw<InvalidOperationException>().WithMessage("*RequireCurrentUser*");
    }

    [Fact]
    public void UnitOfWork_InStrictMode_CommitsWithoutAUser_WhenNoUserFieldIsStamped()
    {
        using var provider = BuildProvider(o => o.RequireCurrentUser = true);
        using var scope = provider.CreateScope();
        var uow = (AppUnitOfWork)scope.ServiceProvider.GetRequiredService<IAppUnitOfWork>();

        uow.Commit();

        uow.Users.Should().Equal([null]);
    }

    private static ServiceProvider BuildProvider(Action<RapidRepoOptions> configureCurrentUser)
    {
        var services = new ServiceCollection();
        services.AddScoped<RequestContext>();
        services.AddRapidRepo(o =>
        {
            o.UseUnitOfWork<IAppUnitOfWork, AppUnitOfWork>();
            configureCurrentUser(o);
        });

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    public sealed class RequestContext
    {
        public Guid? UserId { get; set; }
    }

    public sealed class RequestUserProvider(RequestContext context) : ICurrentUserProvider<Guid>
    {
        public Guid? GetCurrentUserId() => context.UserId;
    }

    public sealed class FixedUserProvider : ICurrentUserProvider<Guid>
    {
        public Guid? GetCurrentUserId() => Guid.Empty;
    }

    public abstract class AbstractUserProvider : ICurrentUserProvider<Guid>
    {
        public abstract Guid? GetCurrentUserId();
    }

    public sealed class AuditedByUser : BaseAuditableEntity<int, Guid>;

    public interface IAppUnitOfWork : IUnitOfWork<Guid>;

    /// <summary>A unit of work that passes its service provider to the base, so it picks up the current user.</summary>
    public sealed class AppUnitOfWork(IServiceProvider serviceProvider)
        : UnitOfWorkBase<Guid>(Guid.Empty, serviceProvider), IAppUnitOfWork
    {
        public List<Guid?> Users { get; } = [];

        /// <summary>Entities each commit stamps as added before "persisting".</summary>
        public List<object> Staged { get; } = [];

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
}
