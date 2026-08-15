using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RapidRepo.Extensions.DependencyInjection.Tests.TestData;
using RapidRepo.Repositories;
using RapidRepo.Repositories.Interfaces;

namespace RapidRepo.Extensions.DependencyInjection.Tests;

/// <summary>
/// The rest of the suite asserts on <see cref="ServiceDescriptor"/>s only. These tests build a real
/// <see cref="ServiceProvider"/> and resolve, which is the only way to catch registrations that are
/// present but not constructible.
/// </summary>
public class ServiceResolutionTests
{
    private static readonly Func<Type, bool> ExcludeAmbiguous =
        t => t.Namespace?.Contains(".Ambiguous") == true;

    // Keeps each context's scan to its own repositories, the way separate modules would be laid out.
    private static readonly Func<Type, bool> ExcludeBillingModule =
        t => t == typeof(BillingGadgetRepository);

    private static readonly Func<Type, bool> ExcludeSalesModule =
        t => t == typeof(SalesWidgetRepository);

    private static ServiceCollection ServicesWithDbContext()
    {
        var services = new ServiceCollection();
        services.AddDbContext<TestDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        return services;
    }

    // ── UseDbContext: single context ───────────────────────────────────────────

    [Fact]
    public void UseDbContext_RegisterGenericRepositories_ResolvingIRepository_Succeeds()
    {
        var services = ServicesWithDbContext();

        services.AddRapidRepo(o =>
        {
            o.UseDbContext<TestDbContext>();
            o.RegisterGenericRepositories = true;
            o.Exclude(ExcludeAmbiguous);
        });

        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();

        var act = () => scope.ServiceProvider.GetRequiredService<IRepository<Widget, int>>();

        act.Should().NotThrow();
    }

    [Fact]
    public void UseDbContext_ScannedRepository_ResolvingUserDefinedInterface_Succeeds()
    {
        var services = ServicesWithDbContext();

        services.AddRapidRepo(o =>
        {
            o.UseDbContext<TestDbContext>();
            o.ScanAssembliesContaining<WidgetRepository>();
            o.Exclude(ExcludeAmbiguous);
        });

        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();

        var act = () => scope.ServiceProvider.GetRequiredService<IWidgetRepository>();

        act.Should().NotThrow();
    }

    [Fact]
    public void UseDbContext_ResolvingUnitOfWork_Succeeds()
    {
        var services = ServicesWithDbContext();

        services.AddRapidRepo(o =>
        {
            o.UseDbContext<TestDbContext>();
            o.ScanAssembliesContaining<WidgetRepository>();
            o.Exclude(ExcludeAmbiguous);
            o.UseUnitOfWork<ITestUnitOfWork, TestUnitOfWork>();
        });

        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();

        var act = () => scope.ServiceProvider.GetRequiredService<ITestUnitOfWork>();

        act.Should().NotThrow();
    }

    /// <summary>
    /// The generic repository resolved through <c>UseDbContext</c> must operate on that context, not merely
    /// be constructible. Saving through the context proves the repository enlisted in the same change tracker.
    /// </summary>
    [Fact]
    public async Task UseDbContext_GenericRepository_WritesToTheConfiguredContext()
    {
        var services = ServicesWithDbContext();

        services.AddRapidRepo(o =>
        {
            o.UseDbContext<TestDbContext>();
            o.RegisterGenericRepositories = true;
            o.Exclude(ExcludeAmbiguous);
        });

        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();

        var repository = scope.ServiceProvider.GetRequiredService<IRepository<Widget, int>>();
        var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        await repository.AddAsync(new Widget());
        await context.SaveChangesAsync();

        (await context.Widgets.CountAsync()).Should().Be(1);
    }

    // ── UseDbContext: forwarder behaviour ──────────────────────────────────────

    [Fact]
    public void UseDbContext_WhenNothingNeedsTheBaseDbContext_DoesNotRegisterForwarder()
    {
        var services = new ServiceCollection();
        services.AddDbContext<SalesDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        services.AddRapidRepo(o =>
        {
            o.UseDbContext<SalesDbContext>();
            o.ScanAssembliesContaining<SalesWidgetRepository>();
            o.IncludeType<SalesWidgetRepository>(); // takes SalesDbContext, not DbContext
        });

        // Guard against the assertion below passing simply because nothing was registered.
        services.Should().Contain(d => d.ImplementationType == typeof(SalesWidgetRepository));
        services.Should().NotContain(d => d.ServiceType == typeof(DbContext));
    }

    [Fact]
    public void UseDbContext_WhenSomethingNeedsTheBaseDbContext_RegistersForwarder()
    {
        var services = ServicesWithDbContext();

        services.AddRapidRepo(o =>
        {
            o.UseDbContext<TestDbContext>();
            o.IncludeType<WidgetRepository>(); // takes DbContext
            o.ScanAssembliesContaining<WidgetRepository>();
        });

        services.Should().Contain(d => d.ServiceType == typeof(DbContext));
    }

    [Fact]
    public void UseDbContext_CalledTwiceInOneCall_Throws()
    {
        var services = ServicesWithDbContext();

        var act = () => services.AddRapidRepo(o =>
        {
            o.UseDbContext<TestDbContext>();
            o.UseDbContext<SalesDbContext>();
        });

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already been configured*");
    }

    // ── Multi-context ──────────────────────────────────────────────────────────

    [Fact]
    public void MultiContext_ScannedRepositories_ResolveAgainstTheirOwnContext()
    {
        var services = new ServiceCollection();
        services.AddDbContext<SalesDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddDbContext<BillingDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        services.AddRapidRepo(o =>
        {
            o.UseDbContext<SalesDbContext>();
            o.ScanAssembliesContaining<SalesWidgetRepository>();
            o.IncludeType<SalesWidgetRepository>();
        });

        services.AddRapidRepo(o =>
        {
            o.UseDbContext<BillingDbContext>();
            o.ScanAssembliesContaining<BillingGadgetRepository>();
            o.IncludeType<BillingGadgetRepository>();
        });

        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<ISalesWidgetRepository>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IBillingGadgetRepository>().Should().NotBeNull();
    }

    /// <summary>
    /// The point of the closed-generic registrations: two contexts each get generic repositories for their
    /// own entities, and each writes to its own database.
    /// </summary>
    [Fact]
    public async Task MultiContext_GenericRepositories_BindToTheirOwnContext()
    {
        var services = new ServiceCollection();
        services.AddDbContext<SalesDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddDbContext<BillingDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        services.AddRapidRepo(o =>
        {
            o.UseDbContext<SalesDbContext>();
            o.RegisterGenericRepositories = true;
            o.Exclude(ExcludeAmbiguous);
            o.Exclude(ExcludeBillingModule);
        });

        services.AddRapidRepo(o =>
        {
            o.UseDbContext<BillingDbContext>();
            o.RegisterGenericRepositories = true;
            o.Exclude(ExcludeAmbiguous);
            o.Exclude(ExcludeSalesModule);
        });

        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();

        var widgets = scope.ServiceProvider.GetRequiredService<IRepository<Widget, int>>();
        var gadgets = scope.ServiceProvider.GetRequiredService<IRepository<Gadget, Guid>>();

        widgets.Should().BeOfType<Repository<Widget, int, SalesDbContext>>();
        gadgets.Should().BeOfType<Repository<Gadget, Guid, BillingDbContext>>();

        var sales = scope.ServiceProvider.GetRequiredService<SalesDbContext>();
        var billing = scope.ServiceProvider.GetRequiredService<BillingDbContext>();

        await widgets.AddAsync(new Widget());
        await gadgets.AddAsync(new Gadget { Id = Guid.NewGuid() });
        await sales.SaveChangesAsync();
        await billing.SaveChangesAsync();

        (await sales.Widgets.CountAsync()).Should().Be(1);
        (await billing.Gadgets.CountAsync()).Should().Be(1);
    }

    /// <summary>
    /// Two contexts cannot both back the single base <see cref="DbContext"/> slot. Repositories that take a
    /// <see cref="DbContext"/> parameter are genuinely ambiguous there, so this fails loudly at registration
    /// rather than silently handing the second module the first module's context.
    /// </summary>
    [Fact]
    public void MultiContext_TwoForwardersForDifferentContexts_Throws()
    {
        var services = new ServiceCollection();
        services.AddDbContext<SalesDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddDbContext<BillingDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        services.AddRapidRepo(o =>
        {
            o.UseDbContext<SalesDbContext>();
            o.IncludeType<WidgetRepository>(); // takes DbContext
            o.ScanAssembliesContaining<WidgetRepository>();
        });

        var act = () => services.AddRapidRepo(o =>
        {
            o.UseDbContext<BillingDbContext>();
            o.IncludeType<GadgetRepository>(); // also takes DbContext
            o.ScanAssembliesContaining<WidgetRepository>();
        });

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ambiguous across contexts*");
    }

    [Fact]
    public void MultiContext_RepeatedCallForTheSameContext_DoesNotThrow()
    {
        var services = ServicesWithDbContext();

        void Register() => services.AddRapidRepo(o =>
        {
            o.UseDbContext<TestDbContext>();
            o.ScanAssembliesContaining<WidgetRepository>();
            o.Exclude(ExcludeAmbiguous);
        });

        Register();

        var act = Register;

        act.Should().NotThrow();
    }

    // ── Missing DbContext diagnostics ──────────────────────────────────────────

    /// <summary>
    /// Without <c>UseDbContext</c> the pre-existing behaviour is unchanged: nothing registers the base
    /// <see cref="DbContext"/>, so activation still fails. Kept as a regression guard on the opt-in being
    /// genuinely opt-in.
    /// </summary>
    [Fact]
    public void WithoutUseDbContext_ScannedRepository_StillFailsToResolve()
    {
        var services = ServicesWithDbContext();

        services.AddRapidRepo(o =>
        {
            o.ScanAssembliesContaining<WidgetRepository>();
            o.Exclude(ExcludeAmbiguous);
        });

        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();

        var act = () => scope.ServiceProvider.GetRequiredService<IWidgetRepository>();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Unable to resolve service for type 'Microsoft.EntityFrameworkCore.DbContext'*");
    }

    [Fact]
    public void ThrowOnMissingDbContext_WhenNothingSatisfiesBaseDbContext_Throws()
    {
        var services = ServicesWithDbContext();

        var act = () => services.AddRapidRepo(o =>
        {
            o.ThrowOnMissingDbContext = true;
            o.ScanAssembliesContaining<WidgetRepository>();
            o.Exclude(ExcludeAmbiguous);
        });

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*UseDbContext*");
    }

    [Fact]
    public void ThrowOnMissingDbContext_WhenApplicationRegistersItsOwnForwarder_DoesNotThrow()
    {
        var services = ServicesWithDbContext();
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<TestDbContext>());

        var act = () => services.AddRapidRepo(o =>
        {
            o.ThrowOnMissingDbContext = true;
            o.ScanAssembliesContaining<WidgetRepository>();
            o.Exclude(ExcludeAmbiguous);
        });

        act.Should().NotThrow();
    }

    // ── Control ────────────────────────────────────────────────────────────────

    /// <summary>
    /// The manual forwarder applications had to write before <c>UseDbContext</c> existed keeps working.
    /// </summary>
    [Fact]
    public void WithManualDbContextForwarder_EverythingResolves()
    {
        var services = ServicesWithDbContext();
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<TestDbContext>());

        services.AddRapidRepo(o =>
        {
            o.RegisterGenericRepositories = true;
            o.ScanAssembliesContaining<WidgetRepository>();
            o.Exclude(ExcludeAmbiguous);
            o.UseUnitOfWork<ITestUnitOfWork, TestUnitOfWork>();
        });

        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IRepository<Widget, int>>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IWidgetRepository>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<ITestUnitOfWork>().Should().NotBeNull();
    }
}
