using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RapidRepo.EntityFrameworkCore.Tests.Conformance;
using RapidRepo.Extensions.DependencyInjection;
using RapidRepo.Repositories.Interfaces;
using RapidRepo.Testing.Conformance;
using RapidRepo.UnitOfWork;

namespace RapidRepo.EntityFrameworkCore.Tests.DependencyInjection;

/// <summary>
/// The documented setup, end to end: an <see cref="EfUnitOfWork{TUserKey}"/> that passes its
/// <see cref="IServiceProvider"/> to the base, resolved from a container configured with
/// <c>UseEntityFrameworkCore&lt;TContext&gt;()</c> and a current-user provider, stamps the request's user.
/// </summary>
public sealed class CurrentUserEndToEndTests
{
    private static readonly Guid DefaultUser = ConformanceDefaults.DefaultUser;

    [Fact]
    public async Task CommitWithoutAUser_StampsTheScopesUser()
    {
        using var provider = BuildProvider(o => o.UseCurrentUser<Guid>(sp => sp.GetRequiredService<RequestContext>().UserId));
        var alice = Guid.NewGuid();
        var note = new Note { Title = "created" };

        using (var scope = provider.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<RequestContext>().UserId = alice;
            scope.ServiceProvider.GetRequiredService<IRepository<Note, int>>().Add(note);
            await scope.ServiceProvider.GetRequiredService<IAppUnitOfWork>().CommitAsync();
        }

        Read(provider, note.Id).CreatedBy.Should().Be(alice);
    }

    [Fact]
    public void EachScope_StampsItsOwnUser()
    {
        using var provider = BuildProvider(o => o.UseCurrentUserProvider<RequestUserProvider, Guid>());
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var note = new Note { Title = "draft" };

        InScope(provider, alice, sp => sp.GetRequiredService<IRepository<Note, int>>().Add(note));
        InScope(provider, bob, sp =>
        {
            var repository = sp.GetRequiredService<IRepository<Note, int>>();
            var loaded = repository.GetById(note.Id)!;
            loaded.Title = "final";
            repository.Update(loaded);
        });

        var read = Read(provider, note.Id);
        read.CreatedBy.Should().Be(alice);
        read.ModifiedBy.Should().Be(bob);
    }

    [Fact]
    public void ScopeWithoutAUser_StampsTheDefaultUser()
    {
        using var provider = BuildProvider(o => o.UseCurrentUserProvider<RequestUserProvider, Guid>());
        var note = new Note();

        InScope(provider, user: null, sp => sp.GetRequiredService<IRepository<Note, int>>().Add(note));

        Read(provider, note.Id).CreatedBy.Should().Be(DefaultUser);
    }

    [Fact]
    public void StrictMode_ScopeWithoutAUser_Throws_AndPersistsNothing()
    {
        using var provider = BuildProvider(o =>
        {
            o.UseCurrentUserProvider<RequestUserProvider, Guid>();
            o.RequireCurrentUser = true;
        });

        var act = () => InScope(provider, user: null, sp => sp.GetRequiredService<IRepository<Note, int>>().Add(new Note()));

        act.Should().Throw<InvalidOperationException>().WithMessage("*returned null*RequireCurrentUser*");
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IRepository<Note, int>>().Count().Should().Be(0);
    }

    private static ServiceProvider BuildProvider(Action<RapidRepoOptions> configureCurrentUser)
    {
        var databaseName = $"CurrentUser-{Guid.NewGuid()}";
        var services = new ServiceCollection();
        services.AddScoped<RequestContext>();
        services.AddDbContext<ConformanceDbContext>(o => o.UseInMemoryDatabase(databaseName));
        services.AddRapidRepo(o =>
        {
            o.UseEntityFrameworkCore<ConformanceDbContext>();
            o.RegisterGenericRepositories = true;
            o.UseUnitOfWork<IAppUnitOfWork, AppUnitOfWork>();
            configureCurrentUser(o);
        });

        return services.BuildServiceProvider(validateScopes: true);
    }

    /// <summary>Runs <paramref name="work"/> in a new scope whose request user is <paramref name="user"/>, then commits without a user.</summary>
    private static void InScope(ServiceProvider provider, Guid? user, Action<IServiceProvider> work)
    {
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<RequestContext>().UserId = user;
        work(scope.ServiceProvider);
        scope.ServiceProvider.GetRequiredService<IAppUnitOfWork>().Commit();
    }

    private static Note Read(ServiceProvider provider, int id)
    {
        using var scope = provider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IRepository<Note, int>>().GetById(id)!;
    }

    public sealed class RequestContext
    {
        public Guid? UserId { get; set; }
    }

    public sealed class RequestUserProvider(RequestContext context) : Auditing.ICurrentUserProvider<Guid>
    {
        public Guid? GetCurrentUserId() => context.UserId;
    }

    public interface IAppUnitOfWork : IUnitOfWork<Guid>;

    /// <summary>Written the way the source generator docs show: it passes its service provider to the base.</summary>
    public sealed class AppUnitOfWork(ConformanceDbContext db, IServiceProvider sp)
        : EfUnitOfWork<Guid>(db, DefaultUser, sp), IAppUnitOfWork;
}
