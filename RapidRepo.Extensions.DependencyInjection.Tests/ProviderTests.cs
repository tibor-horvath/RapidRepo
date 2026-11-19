using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RapidRepo.Extensions.DependencyInjection.Tests.TestData;
using RapidRepo.Extensions.DependencyInjection.Tests.TestData.Ambiguous.Provider;

namespace RapidRepo.Extensions.DependencyInjection.Tests;

/// <summary>
/// The provider extension point: what <c>AddRapidRepo</c> leaves to a provider and what it hands over.
/// </summary>
public class ProviderTests
{
    private static readonly Func<Type, bool> OnlyProviderTestData =
        t => t.Namespace == typeof(FakeProvider).Namespace;

    [Fact]
    public void UseProvider_Twice_Throws()
    {
        var act = () => new ServiceCollection().AddRapidRepo(o =>
        {
            o.UseProvider(new FakeProvider());
            o.UseProvider(new FakeProvider());
        });

        act.Should().Throw<InvalidOperationException>().WithMessage("*already been configured*'Fake'*");
    }

    [Fact]
    public void UseProvider_Null_Throws()
    {
        var act = () => new ServiceCollection().AddRapidRepo(o => o.UseProvider(null!));

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RegisterGenericRepositories_WithoutProvider_Throws()
    {
        var act = () => new ServiceCollection().AddRapidRepo(o => o.RegisterGenericRepositories = true);

        act.Should().Throw<InvalidOperationException>().WithMessage("*requires a provider*UseEntityFrameworkCore*");
    }

    [Fact]
    public void Register_ReceivesTheCoreRootInterfaces_FollowedByTheProvidersOwn()
    {
        var provider = new FakeProvider();

        new ServiceCollection().AddRapidRepo(o => o.UseProvider(provider));

        provider.Calls.Should().ContainSingle().Which.RootInterfaces.Should().Equal(
            typeof(RapidRepo.Repositories.Interfaces.IReadOnlyRepository<,>),
            typeof(RapidRepo.Repositories.Interfaces.IWriteRepository<,>),
            typeof(RapidRepo.Repositories.Interfaces.IRepository<,>),
            typeof(IFakeProviderRepository<,>));
    }

    [Fact]
    public void WithoutProvider_RegistersScannedRepositoriesAndUnitsOfWork()
    {
        var services = new ServiceCollection();

        services.AddRapidRepo(o =>
        {
            o.ScanAssembliesContaining<WidgetRepository>();
            o.IncludeType<WidgetRepository>();
            o.UseUnitOfWork<TestUnitOfWork>();
        });

        services.Should().Contain(d => d.ServiceType == typeof(IWidgetRepository));
        services.Should().Contain(d => d.ServiceType == typeof(ITestUnitOfWork));
    }

    [Fact]
    public void Provider_IsCalledOnce_WithWhatTheCallRegistered()
    {
        var provider = new FakeProvider();

        new ServiceCollection().AddRapidRepo(o =>
        {
            o.UseProvider(provider);
            o.ScanAssembliesContaining<SprocketRepository>();
            o.Include(OnlyProviderTestData);
            o.UseUnitOfWork<TestUnitOfWork>();
            o.Lifetime = ServiceLifetime.Transient;
            o.RegisterAsSelf = true;
            o.RegisterGenericRepositories = true;
        });

        var context = provider.Calls.Should().ContainSingle().Subject;
        context.Repositories.Select(r => r.ImplementationType)
            .Should().BeEquivalentTo([typeof(SprocketRepository), typeof(ArchivedSprocketRepository)]);
        context.Repositories.Single(r => r.ImplementationType == typeof(SprocketRepository)).ServiceTypes
            .Should().Equal(typeof(ISprocketRepository));
        context.UnitsOfWork.Should().Equal(new UnitOfWorkRegistration(typeof(ITestUnitOfWork), typeof(TestUnitOfWork)));
        context.ActivatedTypes.Should().Contain([typeof(SprocketRepository), typeof(TestUnitOfWork)]);
        context.Lifetime.Should().Be(ServiceLifetime.Transient);
        context.RegisterAsSelf.Should().BeTrue();
        context.RegisterGenericRepositories.Should().BeTrue();
    }

    [Fact]
    public void Provider_RunsAfterScannedRepositoriesAndUnitsOfWorkAreRegistered()
    {
        var provider = new FakeProvider();

        new ServiceCollection().AddRapidRepo(o =>
        {
            o.UseProvider(provider);
            o.ScanAssembliesContaining<SprocketRepository>();
            o.Include(OnlyProviderTestData);
            o.UseUnitOfWork<TestUnitOfWork>();
        });

        provider.ServiceTypesSeenAtRegistration.Should().Contain([typeof(ISprocketRepository), typeof(ITestUnitOfWork)]);
    }

    [Fact]
    public void ProviderRootInterface_WithoutTheProvider_IsAmbiguous_AndTheErrorPointsAtProviders()
    {
        var act = () => new ServiceCollection().AddRapidRepo(o =>
        {
            o.ScanAssembliesContaining<SprocketRepository>();
            o.Include(OnlyProviderTestData);
        });

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*IFakeProviderRepository*configure that provider*");
    }

    [Fact]
    public void ProviderRootInterface_WithTheProvider_IsTreatedAsARoot()
    {
        var services = new ServiceCollection();

        services.AddRapidRepo(o =>
        {
            o.UseProvider(new FakeProvider());
            o.ScanAssembliesContaining<SprocketRepository>();
            o.Include(OnlyProviderTestData);
        });

        services.Should().NotContain(d =>
            d.ServiceType.IsGenericType && d.ServiceType.GetGenericTypeDefinition() == typeof(IFakeProviderRepository<,>));
        services.Should().Contain(d =>
            d.ServiceType == typeof(ISprocketRepository) && d.ImplementationType == typeof(SprocketRepository));
        services.Should().Contain(d =>
            d.ServiceType == typeof(IArchivedSprocketRepository) && d.ImplementationType == typeof(ArchivedSprocketRepository));
    }
}
