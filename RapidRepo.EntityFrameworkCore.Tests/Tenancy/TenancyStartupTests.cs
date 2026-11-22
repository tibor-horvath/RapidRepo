using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RapidRepo.Extensions;
using RapidRepo.Tenancy;
using RapidRepo.Tenancy.Host;

namespace RapidRepo.EntityFrameworkCore.Tests.Tenancy;

/// <summary>
/// The startup validation, the registered scopes, and a pooled context: what <c>AddRapidRepo</c> sets up for tenancy.
/// </summary>
public sealed class TenancyStartupTests : IDisposable
{
    private readonly SqliteDatabase _database = new();

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task StartAsync_WithAValidContext_Succeeds()
    {
        await using var provider = Build<OrderContext>(o => o.UseCurrentTenantProvider<TestTenant, Guid>());

        var hosted = provider.GetServices<IHostedService>().Should().ContainSingle().Subject;

        await hosted.Invoking(h => h.StartAsync(CancellationToken.None)).Should().NotThrowAsync();
    }

    [Fact]
    public async Task StartAsync_WithAMisconfiguredContext_FailsTheHost_ListingEveryProblem()
    {
        await using var provider = Build<MissingFilterContext>(o => o.UseCurrentTenantProvider<TestTenant, Guid>());
        var hosted = provider.GetServices<IHostedService>().Single();

        var start = () => hosted.StartAsync(CancellationToken.None);

        var exception = await start.Should().ThrowAsync<RapidRepoConfigurationException>().WithMessage("*3 problems*");
        exception.Which.Problems.Should().HaveCount(3)
            .And.Contain(p => p.StartsWith("MissingFilterContext: Order is a tenant entity without the tenant query filter"));
    }

    [Fact]
    public async Task ValidateRapidRepoTenancy_RunsTheSameChecks_WithoutAHost()
    {
        await using var provider = Build<MissingFilterContext>(o =>
        {
            o.UseCurrentTenantProvider<TestTenant, Guid>();
            o.ValidateTenancyOnStartup = false;
        });

        provider.GetServices<IHostedService>().Should().BeEmpty();
        var validate = provider.ValidateRapidRepoTenancy;
        validate.Should().Throw<InvalidOperationException>().WithMessage("*without the tenant query filter*");
    }

    [Fact]
    public async Task ValidateRapidRepoTenancy_ReportsAMissingTenantProvider()
    {
        await using var provider = Build<OrderContext>(_ => { });

        var validate = provider.ValidateRapidRepoTenancy;

        validate.Should().Throw<InvalidOperationException>().WithMessage("*no ICurrentTenantProvider<Guid> is registered*");
    }

    [Fact]
    public async Task WithoutATenantProvider_NothingIsRegistered_AndTheContextIsNotBuiltAtStartup()
    {
        // Counts the contexts built by this provider only: the options are built once per context.
        var constructed = 0;
        var services = new ServiceCollection();
        services.AddDbContext<PlainContext>(o =>
        {
            Interlocked.Increment(ref constructed);
            o.UseSqlite(_database.Connection);
        });
        services.AddRapidRepo(o => o.UseEntityFrameworkCore<PlainContext>());
        await using var provider = services.BuildServiceProvider();

        foreach (var hosted in provider.GetServices<IHostedService>())
            await hosted.StartAsync(CancellationToken.None);

        provider.GetServices<IHostedService>().Should().BeEmpty();
        provider.GetService<ITenantScope<Guid>>().Should().BeNull();
        provider.GetService<ITenantHostScope>().Should().BeNull();
        constructed.Should().Be(0);
    }

    [Fact]
    public async Task UseCurrentTenant_RegistersTheDelegate_AndTheScopes()
    {
        var tenant = Guid.NewGuid();
        await using var provider = Build<OrderContext>(o => o.UseCurrentTenant<Guid>(_ => tenant));
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<ICurrentTenantProvider<Guid>>().GetCurrentTenantId().Should().Be(tenant);
        scope.ServiceProvider.GetRequiredService<OrderContext>().CurrentTenantId.Should().Be(tenant);
        provider.GetRequiredService<ITenantScope<Guid>>().Should().NotBeNull();
        provider.GetRequiredService<ITenantHostScope>().Should().NotBeNull();
    }

    [Fact]
    public async Task HostScope_IsDenied_WhenTheRegisteredPolicySaysSo()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITenantHostScopePolicy>(new DenyAll());
        services.AddDbContext<OrderContext>(o => o.UseSqlite(_database.Connection));
        services.AddRapidRepo(o => o.UseEntityFrameworkCore<OrderContext>().UseCurrentTenantProvider<TestTenant, Guid>());
        await using var provider = services.BuildServiceProvider();

        var begin = () => provider.GetRequiredService<ITenantHostScope>().BeginHostScope();

        begin.Should().Throw<TenantViolationException>().WithMessage("*denied*DenyAll*");
        AmbientTenant.IsHostScope.Should().BeFalse();
    }

    [Fact]
    public async Task Override_FromTheContainer_IsHonouredByTheContext()
    {
        var tenant = new TestTenant { Current = Guid.NewGuid() };
        var other = Guid.NewGuid();
        var services = new ServiceCollection();
        services.AddSingleton(tenant);
        services.AddDbContext<OrderContext>(o => o.UseSqlite(_database.Connection));
        services.AddRapidRepo(o => o.UseEntityFrameworkCore<OrderContext>().UseCurrentTenant<Guid>(sp => sp.GetRequiredService<TestTenant>().Current));
        await using var provider = services.BuildServiceProvider();

        using (var seedScope = provider.CreateScope())
        {
            var seed = seedScope.ServiceProvider.GetRequiredService<OrderContext>();
            seed.Database.EnsureCreated();
            using var host = provider.GetRequiredService<ITenantHostScope>().BeginHostScope();
            seed.Orders.AddRange(
                new Order { TenantId = tenant.Current, Title = "own" },
                new Order { TenantId = other, Title = "other" });
            seed.SaveChanges();
        }

        List<string> Titles()
        {
            using var scope = provider.CreateScope();
            return new EfRepository<Order, int>(scope.ServiceProvider.GetRequiredService<OrderContext>())
                .GetAll().Select(o => o.Title).ToList();
        }

        using (provider.GetRequiredService<ITenantScope<Guid>>().Override(other))
            Titles().Should().Equal("other");

        Titles().Should().Equal("own");
    }

    [Fact]
    public async Task APooledContext_ReadsTheTenantPerQuery_AcrossLeases()
    {
        var services = new ServiceCollection();
        services.AddDbContextPool<PooledOrderContext>(o => o.UseSqlite(_database.Connection));
        await using var provider = services.BuildServiceProvider();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        using (var seedScope = provider.CreateScope())
        {
            var seed = seedScope.ServiceProvider.GetRequiredService<PooledOrderContext>();
            seed.Database.EnsureCreated();
            seed.AddRange(new Order { TenantId = tenantA }, new Order { TenantId = tenantA }, new Order { TenantId = tenantB });
            seed.SaveChanges();
        }

        PooledOrderContext first, second;
        int countA, countB;

        using (var scope = provider.CreateScope())
        {
            PooledOrderContext.RequestTenant.Value = tenantA;
            first = scope.ServiceProvider.GetRequiredService<PooledOrderContext>();
            countA = new EfRepository<Order, int>(first).Count();
        }

        using (var scope = provider.CreateScope())
        {
            PooledOrderContext.RequestTenant.Value = tenantB;
            second = scope.ServiceProvider.GetRequiredService<PooledOrderContext>();
            countB = new EfRepository<Order, int>(second).Count();
        }

        second.Should().BeSameAs(first, "the pool hands out the same instance again");
        countA.Should().Be(2);
        countB.Should().Be(1);
    }

    private ServiceProvider Build<TContext>(Action<Extensions.DependencyInjection.RapidRepoOptions> configure)
        where TContext : DbContext
    {
        var services = new ServiceCollection();
        services.AddDbContext<TContext>(o => o.UseSqlite(_database.Connection));
        services.AddRapidRepo(o =>
        {
            o.UseEntityFrameworkCore<TContext>();
            configure(o);
        });

        return services.BuildServiceProvider();
    }

    private sealed class DenyAll : ITenantHostScopePolicy
    {
        public bool IsAllowed(TenantHostScopeRequest request) => false;
    }
}

/// <summary>
/// A pooled context cannot take scoped services, so it reads the tenant from ambient state, the way a provider over
/// <c>IHttpContextAccessor</c> would.
/// </summary>
public sealed class PooledOrderContext(DbContextOptions<PooledOrderContext> options) : DbContext(options), ITenantDbContext<Guid>
{
    public static readonly AsyncLocal<Guid> RequestTenant = new();

    public Guid CurrentTenantId => RequestTenant.Value;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>().HasMany(o => o.Lines).WithOne().HasForeignKey(l => l.OrderId);
        modelBuilder.AddSoftDeleteQueryFilters();
        modelBuilder.AddTenantQueryFilters(this);
    }
}
