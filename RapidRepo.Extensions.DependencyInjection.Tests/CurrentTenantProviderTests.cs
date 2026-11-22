using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RapidRepo.Extensions.DependencyInjection;
using RapidRepo.Tenancy;
using RapidRepo.Tenancy.Host;

namespace RapidRepo.Extensions.DependencyInjection.Tests;

/// <summary>
/// <c>UseCurrentTenantProvider</c> / <c>UseCurrentTenant</c> and what they register: the provider with the units of
/// work's lifetime, the tenant scopes, and the startup validation.
/// </summary>
public class CurrentTenantProviderTests
{
    [Fact]
    public void UseCurrentTenantProvider_RegistersTheProvider_WithTheConfiguredLifetime()
    {
        var services = new ServiceCollection();

        services.AddRapidRepo(o =>
        {
            o.Lifetime = ServiceLifetime.Transient;
            o.UseCurrentTenantProvider<FixedTenantProvider, Guid>();
        });

        services.Should().ContainSingle(d => d.ServiceType == typeof(ICurrentTenantProvider<Guid>))
            .Which.Lifetime.Should().Be(ServiceLifetime.Transient);
    }

    [Fact]
    public void UseCurrentTenant_RegistersTheDelegate_ResolvedInTheScope()
    {
        var services = new ServiceCollection();
        services.AddScoped<Request>();
        services.AddRapidRepo(o => o.UseCurrentTenant<string>(sp => sp.GetRequiredService<Request>().Tenant));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<Request>().Tenant = "acme";

        scope.ServiceProvider.GetRequiredService<ICurrentTenantProvider<string>>().GetCurrentTenantId().Should().Be("acme");
    }

    [Fact]
    public void ATenantProvider_RegistersTheScopes_AndTheStartupValidation()
    {
        var services = new ServiceCollection();

        services.AddRapidRepo(o => o.UseCurrentTenantProvider<FixedTenantProvider, Guid>());

        services.Should().Contain(d => d.ServiceType == typeof(ITenantScope<>));
        services.Should().Contain(d => d.ServiceType == typeof(ITenantHostScope));
        services.Should().ContainSingle(d => d.ServiceType == typeof(IHostedService));
    }

    [Fact]
    public void ValidateTenancyOnStartup_Off_RegistersNoHostedService()
    {
        var services = new ServiceCollection();

        services.AddRapidRepo(o =>
        {
            o.UseCurrentTenantProvider<FixedTenantProvider, Guid>();
            o.ValidateTenancyOnStartup = false;
        });

        services.Should().NotContain(d => d.ServiceType == typeof(IHostedService));
    }

    [Fact]
    public void DoubleCall_DoesNotDuplicateTheHostedService()
    {
        var services = new ServiceCollection();

        services.AddRapidRepo(o => o.UseCurrentTenantProvider<FixedTenantProvider, Guid>());
        services.AddRapidRepo(o => o.UseCurrentTenantProvider<FixedTenantProvider, Guid>());

        services.Where(d => d.ServiceType == typeof(IHostedService)).Should().HaveCount(1);
    }

    [Fact]
    public void WithoutATenantProvider_NothingTenancyRelatedIsRegistered()
    {
        var services = new ServiceCollection();

        services.AddRapidRepo(_ => { });

        services.Should().BeEmpty();
    }

    [Fact]
    public void SecondProvider_ForTheSameTenantKey_Throws()
    {
        var options = new RapidRepoOptions().UseCurrentTenantProvider<FixedTenantProvider, Guid>();

        var act = () => options.UseCurrentTenant<Guid>(_ => Guid.Empty);

        act.Should().Throw<InvalidOperationException>().WithMessage("*already been configured*'Guid'*");
    }

    [Fact]
    public void AbstractProvider_Throws()
    {
        var act = () => new RapidRepoOptions().UseCurrentTenantProvider<AbstractTenantProvider, Guid>();

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void NullDelegate_Throws()
    {
        var act = () => new RapidRepoOptions().UseCurrentTenant<Guid>(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ValidateRapidRepoTenancy_WithNoValidators_Succeeds()
    {
        var services = new ServiceCollection();
        services.AddRapidRepo(o => o.UseCurrentTenantProvider<FixedTenantProvider, Guid>());
        using var provider = services.BuildServiceProvider();

        var validate = provider.ValidateRapidRepoTenancy;

        validate.Should().NotThrow();
    }

    [Fact]
    public async Task StartAsync_AggregatesTheProblemsOfEveryValidator()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRapidRepoStartupValidator>(new FailingValidator("first problem", "second problem"));
        services.AddSingleton<IRapidRepoStartupValidator>(new FailingValidator("third problem"));
        services.AddRapidRepo(o => o.UseCurrentTenantProvider<FixedTenantProvider, Guid>());
        using var provider = services.BuildServiceProvider();

        var start = () => provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken.None);

        (await start.Should().ThrowAsync<RapidRepoConfigurationException>().WithMessage("*3 problems*"))
            .Which.Problems.Should().Equal("first problem", "second problem", "third problem");
    }

    private sealed class Request
    {
        public string? Tenant { get; set; }
    }

    private sealed class FixedTenantProvider : ICurrentTenantProvider<Guid>
    {
        public Guid GetCurrentTenantId() => Guid.Empty;
    }

    private abstract class AbstractTenantProvider : ICurrentTenantProvider<Guid>
    {
        public abstract Guid GetCurrentTenantId();
    }

    private sealed class FailingValidator(params string[] problems) : IRapidRepoStartupValidator
    {
        public IReadOnlyList<string> Validate(IServiceProvider services) => problems;
    }
}
