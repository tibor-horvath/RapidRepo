using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using RapidRepo.EntityFrameworkCore.Tests.DependencyInjection.TestData;
using RapidRepo.Repositories.Interfaces;

namespace RapidRepo.EntityFrameworkCore.Tests.DependencyInjection;

/// <summary>
/// The open-generic fallback the EF provider registers for <c>RegisterGenericRepositories</c> when the call is not
/// bound to a context.
/// </summary>
public class GenericRepositoryRegistrationTests
{
    private static readonly Func<Type, bool> ExcludeAmbiguous =
        t => t.Namespace?.Contains(".Ambiguous") == true
            || t.Namespace?.StartsWith("RapidRepo.EntityFrameworkCore.Tests.DependencyInjection.TestData", StringComparison.Ordinal) != true;

    [Fact]
    public void RegisterGenericRepositories_False_DoesNotRegisterOpenGenericDescriptors()
    {
        var services = new ServiceCollection();

        services.AddRapidRepo(o => { /* RegisterGenericRepositories defaults to false */ });

        services.Should().NotContain(d => d.ServiceType == typeof(IRepository<,>));
        services.Should().NotContain(d => d.ServiceType == typeof(IReadOnlyRepository<,>));
        services.Should().NotContain(d => d.ServiceType == typeof(IWriteRepository<,>));
    }

    [Fact]
    public void RegisterGenericRepositories_True_RegistersIRepositoryOpenGeneric()
    {
        var services = new ServiceCollection();

        services.AddRapidRepo(o => o.UseEntityFrameworkCore().RegisterGenericRepositories = true);

        services.Should().Contain(d =>
            d.ServiceType == typeof(IRepository<,>) &&
            d.ImplementationType == typeof(EfRepository<,>));
    }

    [Fact]
    public void RegisterGenericRepositories_True_RegistersIReadOnlyRepositoryOpenGeneric()
    {
        var services = new ServiceCollection();

        services.AddRapidRepo(o => o.UseEntityFrameworkCore().RegisterGenericRepositories = true);

        services.Should().Contain(d =>
            d.ServiceType == typeof(IReadOnlyRepository<,>) &&
            d.ImplementationType == typeof(EfRepository<,>));
    }

    [Fact]
    public void RegisterGenericRepositories_True_RegistersIWriteRepositoryOpenGeneric()
    {
        var services = new ServiceCollection();

        services.AddRapidRepo(o => o.UseEntityFrameworkCore().RegisterGenericRepositories = true);

        services.Should().Contain(d =>
            d.ServiceType == typeof(IWriteRepository<,>) &&
            d.ImplementationType == typeof(EfRepository<,>));
    }

    [Fact]
    public void RegisterGenericRepositories_True_RespectsConfiguredLifetime()
    {
        var services = new ServiceCollection();

        services.AddRapidRepo(o =>
        {
            o.UseEntityFrameworkCore();
            o.Lifetime = ServiceLifetime.Transient;
            o.RegisterGenericRepositories = true;
        });

        services.Should().Contain(d =>
            d.ServiceType == typeof(IRepository<,>) &&
            d.Lifetime == ServiceLifetime.Transient);
    }

    [Fact]
    public void RegisterGenericRepositories_True_DoubleCall_DoesNotDuplicateOpenGenericDescriptors()
    {
        var services = new ServiceCollection();

        services.AddRapidRepo(o => o.UseEntityFrameworkCore().RegisterGenericRepositories = true);
        services.AddRapidRepo(o => o.UseEntityFrameworkCore().RegisterGenericRepositories = true);

        services.Count(d => d.ServiceType == typeof(IRepository<,>)).Should().Be(1);
        services.Count(d => d.ServiceType == typeof(IReadOnlyRepository<,>)).Should().Be(1);
        services.Count(d => d.ServiceType == typeof(IWriteRepository<,>)).Should().Be(1);
    }

    [Fact]
    public void RegisterGenericRepositories_True_CustomRepositoryDescriptorCoexists()
    {
        var services = new ServiceCollection();

        services.AddRapidRepo(o =>
        {
            o.UseEntityFrameworkCore();
            o.ScanAssembliesContaining<WidgetRepository>();
            o.Exclude(ExcludeAmbiguous);
            o.RegisterGenericRepositories = true;
        });

        // Specific registration from scanning is untouched
        services.Should().Contain(d =>
            d.ServiceType == typeof(IWidgetRepository) &&
            d.ImplementationType == typeof(WidgetRepository));

        // Open-generic fallback is also present as a separate descriptor
        services.Should().Contain(d =>
            d.ServiceType == typeof(IRepository<,>) &&
            d.ImplementationType == typeof(EfRepository<,>));
    }

    [Fact]
    public void RegisterGenericRepositories_True_AlsoRegistersTheEfInterfaces()
    {
        var services = new ServiceCollection();

        services.AddRapidRepo(o =>
        {
            o.UseEntityFrameworkCore();
            o.RegisterGenericRepositories = true;
        });

        services.Should().Contain(d =>
            d.ServiceType == typeof(IEfRepository<,>) && d.ImplementationType == typeof(EfRepository<,>));
        services.Should().Contain(d =>
            d.ServiceType == typeof(IEfReadOnlyRepository<,>) && d.ImplementationType == typeof(EfRepository<,>));
    }
}
