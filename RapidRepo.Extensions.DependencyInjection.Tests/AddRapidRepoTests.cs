using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RapidRepo.Extensions.DependencyInjection.Tests.TestData;
using RapidRepo.Extensions.DependencyInjection.Tests.TestData.Ambiguous;
using RapidRepo.Repositories;
using RapidRepo.Repositories.Interfaces;
using RapidRepo.UnitOfWork;

namespace RapidRepo.Extensions.DependencyInjection.Tests;

/// <summary>
/// Happy-path tests scan the full test assembly but exclude the .Ambiguous namespace,
/// which contains deliberately conflicting types used only by the ambiguity tests.
/// </summary>
public class AddRapidRepoTests
{
    // Shared exclude predicate used in all happy-path tests that scan the full assembly.
    private static readonly Func<Type, bool> ExcludeAmbiguous =
        t => t.Namespace?.Contains(".Ambiguous") == true;

    // ── Basic scanning ─────────────────────────────────────────────────────────

    [Fact]
    public void BasicScan_RegistersUserDefinedInterfaces()
    {
        var services = new ServiceCollection();

        services.AddRapidRepo(o =>
        {
            o.ScanAssembliesContaining<WidgetRepository>();
            o.Exclude(ExcludeAmbiguous);
        });

        services.Should().Contain(d => d.ServiceType == typeof(IWidgetRepository) && d.ImplementationType == typeof(WidgetRepository));
        services.Should().Contain(d => d.ServiceType == typeof(IGadgetRepository) && d.ImplementationType == typeof(GadgetRepository));
    }

    [Fact]
    public void BasicScan_DoesNotRegisterRootInterfaces()
    {
        var services = new ServiceCollection();

        services.AddRapidRepo(o =>
        {
            o.ScanAssembliesContaining<WidgetRepository>();
            o.Exclude(ExcludeAmbiguous);
        });

        services.Should().NotContain(d =>
            d.ServiceType.IsGenericType && (
                d.ServiceType.GetGenericTypeDefinition() == typeof(IReadOnlyRepository<,>) ||
                d.ServiceType.GetGenericTypeDefinition() == typeof(IWriteRepository<,>) ||
                d.ServiceType.GetGenericTypeDefinition() == typeof(IRepository<,>)));
    }

    [Fact]
    public void BasicScan_NoInterfaceRepository_NotRegisteredAgainstAnyInterface()
    {
        var services = new ServiceCollection();

        services.AddRapidRepo(o =>
        {
            o.ScanAssembliesContaining<WidgetRepository>();
            o.Exclude(ExcludeAmbiguous);
        });

        services.Should().NotContain(d => d.ImplementationType == typeof(NoInterfaceRepository));
    }

    // ── Lifetime ───────────────────────────────────────────────────────────────

    [Fact]
    public void DefaultLifetime_IsScoped()
    {
        var services = new ServiceCollection();

        services.AddRapidRepo(o =>
        {
            o.ScanAssembliesContaining<WidgetRepository>();
            o.Exclude(ExcludeAmbiguous);
        });

        services.Where(d => d.ImplementationType == typeof(WidgetRepository))
            .Should().AllSatisfy(d => d.Lifetime.Should().Be(ServiceLifetime.Scoped));
    }

    [Fact]
    public void TransientLifetime_AppliedToAllDescriptors()
    {
        var services = new ServiceCollection();

        services.AddRapidRepo(o =>
        {
            o.ScanAssembliesContaining<WidgetRepository>();
            o.Lifetime = ServiceLifetime.Transient;
            o.Exclude(ExcludeAmbiguous);
        });

        services.Where(d => d.ImplementationType == typeof(WidgetRepository))
            .Should().AllSatisfy(d => d.Lifetime.Should().Be(ServiceLifetime.Transient));
    }

    [Fact]
    public void SingletonLifetime_EmitsWarningAndDoesNotThrow_ByDefault()
    {
        var services = new ServiceCollection();

        var act = () => services.AddRapidRepo(o =>
        {
            o.ScanAssembliesContaining<WidgetRepository>();
            o.Lifetime = ServiceLifetime.Singleton;
            o.Exclude(ExcludeAmbiguous);
        });

        act.Should().NotThrow();
    }

    [Fact]
    public void SingletonLifetime_Throws_WhenThrowOnSingletonMisuseIsTrue()
    {
        var services = new ServiceCollection();

        var act = () => services.AddRapidRepo(o =>
        {
            o.ScanAssembliesContaining<WidgetRepository>();
            o.Lifetime = ServiceLifetime.Singleton;
            o.ThrowOnSingletonMisuse = true;
            o.Exclude(ExcludeAmbiguous);
        });

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Singleton*");
    }

    // ── RegisterAsSelf ─────────────────────────────────────────────────────────

    [Fact]
    public void RegisterAsSelf_False_ConcreteTypeNotInCollection()
    {
        var services = new ServiceCollection();

        services.AddRapidRepo(o =>
        {
            o.ScanAssembliesContaining<WidgetRepository>();
            o.RegisterAsSelf = false;
            o.Exclude(ExcludeAmbiguous);
        });

        services.Should().NotContain(d => d.ServiceType == typeof(WidgetRepository) && d.ImplementationType == typeof(WidgetRepository));
    }

    [Fact]
    public void RegisterAsSelf_True_ConcreteTypeInCollection()
    {
        var services = new ServiceCollection();

        services.AddRapidRepo(o =>
        {
            o.ScanAssembliesContaining<WidgetRepository>();
            o.RegisterAsSelf = true;
            o.Exclude(ExcludeAmbiguous);
        });

        services.Should().Contain(d => d.ServiceType == typeof(WidgetRepository) && d.ImplementationType == typeof(WidgetRepository));
    }

    // ── Ambiguity ──────────────────────────────────────────────────────────────

    [Fact]
    public void AmbiguousRegistration_Throws_ByDefault()
    {
        var services = new ServiceCollection();

        var act = () => services.AddRapidRepo(o =>
            o.ScanAssembliesContaining<GizmoRepositoryA>());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Ambiguous*");
    }

    [Fact]
    public void AmbiguousRegistration_DoesNotThrow_WhenDisabled()
    {
        var services = new ServiceCollection();

        var act = () => services.AddRapidRepo(o =>
        {
            o.ScanAssembliesContaining<GizmoRepositoryA>();
            o.ThrowOnAmbiguousRegistration = false;
        });

        act.Should().NotThrow();
    }

    // ── Filters ────────────────────────────────────────────────────────────────

    [Fact]
    public void ExcludeFilter_RemovesMatchingTypes()
    {
        var services = new ServiceCollection();

        services.AddRapidRepo(o =>
        {
            o.ScanAssembliesContaining<WidgetRepository>();
            o.Exclude(ExcludeAmbiguous);
            o.Exclude(t => t == typeof(WidgetRepository));
        });

        services.Should().NotContain(d => d.ImplementationType == typeof(WidgetRepository));
    }

    [Fact]
    public void IncludeFilter_OnlyRegistersMatchingTypes()
    {
        var services = new ServiceCollection();

        services.AddRapidRepo(o =>
        {
            o.ScanAssembliesContaining<WidgetRepository>();
            o.Include(t => t == typeof(GadgetRepository));
        });

        services.Should().Contain(d => d.ImplementationType == typeof(GadgetRepository));
        services.Should().NotContain(d => d.ImplementationType == typeof(WidgetRepository));
    }

    [Fact]
    public void ExcludeWinsOverInclude()
    {
        var services = new ServiceCollection();

        services.AddRapidRepo(o =>
        {
            o.ScanAssembliesContaining<WidgetRepository>();
            o.Exclude(ExcludeAmbiguous);
            o.Include(t => t == typeof(WidgetRepository));
            o.Exclude(t => t == typeof(WidgetRepository));
        });

        services.Should().NotContain(d => d.ImplementationType == typeof(WidgetRepository));
    }

    // ── Skipped types ──────────────────────────────────────────────────────────

    [Fact]
    public void AbstractClass_IsNotRegistered()
    {
        var services = new ServiceCollection();

        services.AddRapidRepo(o =>
        {
            o.ScanAssembliesContaining<WidgetRepository>();
            o.Exclude(ExcludeAmbiguous);
        });

        services.Should().NotContain(d => d.ImplementationType == typeof(AbstractRepository));
    }

    [Fact]
    public void OpenGenericClass_IsNotRegistered()
    {
        var services = new ServiceCollection();

        services.AddRapidRepo(o =>
        {
            o.ScanAssembliesContaining<WidgetRepository>();
            o.Exclude(ExcludeAmbiguous);
        });

        services.Should().NotContain(d => d.ImplementationType == typeof(OpenRepository<,>));
    }

    // ── Empty assembly ─────────────────────────────────────────────────────────

    [Fact]
    public void EmptyAssemblyList_DoesNotThrowAndRegistersNothing()
    {
        var services = new ServiceCollection();

        var act = () => services.AddRapidRepo(o => { /* no assemblies */ });

        act.Should().NotThrow();
        services.Should().BeEmpty();
    }

    // ── Idempotency ────────────────────────────────────────────────────────────

    [Fact]
    public void DoubleCall_DoesNotDuplicateDescriptors()
    {
        var services = new ServiceCollection();

        services.AddRapidRepo(o =>
        {
            o.ScanAssembliesContaining<GadgetRepository>();
            o.Exclude(ExcludeAmbiguous);
        });
        services.AddRapidRepo(o =>
        {
            o.ScanAssembliesContaining<GadgetRepository>();
            o.Exclude(ExcludeAmbiguous);
        });

        services.Count(d => d.ImplementationType == typeof(GadgetRepository))
            .Should().Be(1);
    }

    [Fact]
    public void ScanAssembliesContaining_Twice_DeduplicatesAssembly()
    {
        var services = new ServiceCollection();

        services.AddRapidRepo(o =>
        {
            o.ScanAssembliesContaining<WidgetRepository>();
            o.ScanAssembliesContaining<WidgetRepository>(); // duplicate — should be ignored
            o.Exclude(ExcludeAmbiguous);
        });

        // Without dedup, two passes would produce 2 descriptors for IGadgetRepository;
        // TryAddEnumerable prevents the duplicate, so count must be exactly 1.
        services.Count(d => d.ImplementationType == typeof(GadgetRepository))
            .Should().Be(1);
    }

    // ── Multi-assembly ─────────────────────────────────────────────────────────

    [Fact]
    public void MultiAssembly_RegistersFromAllAssemblies()
    {
        var services = new ServiceCollection();

        services.AddRapidRepo(o =>
        {
            o.ScanAssemblies(typeof(WidgetRepository).Assembly);
            o.Exclude(ExcludeAmbiguous);
        });

        services.Should().Contain(d => d.ImplementationType == typeof(WidgetRepository));
        services.Should().Contain(d => d.ImplementationType == typeof(GadgetRepository));
    }

    // ── ScanCallingAssembly ────────────────────────────────────────────────────

    [Fact]
    public void ScanCallingAssembly_ResolvesCallerAssembly()
    {
        var services = new ServiceCollection();

        services.AddRapidRepo(o =>
        {
            o.ScanCallingAssembly();
            o.Exclude(ExcludeAmbiguous);
        });

        // WidgetRepository lives in the test assembly — it should be discovered.
        services.Should().Contain(d => d.ImplementationType == typeof(WidgetRepository));
    }

    // ── Unit of Work ───────────────────────────────────────────────────────────

    [Fact]
    public void UseUnitOfWork_RegistersUserInterface()
    {
        var services = new ServiceCollection();

        services.AddRapidRepo(o =>
            o.UseUnitOfWork<ITestUnitOfWork, TestUnitOfWork>());

        services.Should().Contain(d =>
            d.ServiceType == typeof(ITestUnitOfWork) &&
            d.ImplementationType == typeof(TestUnitOfWork));
    }

    [Fact]
    public void UseUnitOfWork_AlsoForwardsClosedIUnitOfWork()
    {
        var services = new ServiceCollection();

        services.AddRapidRepo(o =>
            o.UseUnitOfWork<ITestUnitOfWork, TestUnitOfWork>());

        services.Should().Contain(d =>
            d.ServiceType == typeof(IUnitOfWork<Guid>) &&
            d.ImplementationType == typeof(TestUnitOfWork));
    }

    [Fact]
    public void UseUnitOfWork_DirectlyWithIUnitOfWork_RegistersOnlyThatInterface()
    {
        var services = new ServiceCollection();

        services.AddRapidRepo(o =>
            o.UseUnitOfWork<IUnitOfWork<Guid>, TestUnitOfWork>());

        services.Should().Contain(d =>
            d.ServiceType == typeof(IUnitOfWork<Guid>) &&
            d.ImplementationType == typeof(TestUnitOfWork));

        services.Should().NotContain(d => d.ServiceType == typeof(ITestUnitOfWork));
    }

    [Fact]
    public void UseUnitOfWork_RespectsConfiguredLifetime()
    {
        var services = new ServiceCollection();

        services.AddRapidRepo(o =>
        {
            o.Lifetime = ServiceLifetime.Transient;
            o.UseUnitOfWork<ITestUnitOfWork, TestUnitOfWork>();
        });

        services.Should().Contain(d =>
            d.ServiceType == typeof(ITestUnitOfWork) &&
            d.Lifetime == ServiceLifetime.Transient);
    }

    [Fact]
    public void UseUnitOfWork_MultipleInOneCall_AllRegistered()
    {
        var services = new ServiceCollection();

        services.AddRapidRepo(o =>
        {
            o.UseUnitOfWork<ITestUnitOfWork, TestUnitOfWork>();
            o.UseUnitOfWork<ISecondUnitOfWork, SecondUnitOfWork>();
        });

        services.Should().Contain(d =>
            d.ServiceType == typeof(ITestUnitOfWork) &&
            d.ImplementationType == typeof(TestUnitOfWork));
        services.Should().Contain(d =>
            d.ServiceType == typeof(ISecondUnitOfWork) &&
            d.ImplementationType == typeof(SecondUnitOfWork));
    }

    [Fact]
    public void UseUnitOfWork_SameInterfaceTwice_Throws()
    {
        var services = new ServiceCollection();

        var act = () => services.AddRapidRepo(o =>
        {
            o.UseUnitOfWork<ITestUnitOfWork, TestUnitOfWork>();
            o.UseUnitOfWork<ITestUnitOfWork, TestUnitOfWork>();
        });

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void UseUnitOfWork_NonInterfaceTInterface_Throws()
    {
        var services = new ServiceCollection();

        var act = () => services.AddRapidRepo(o =>
            o.UseUnitOfWork<TestUnitOfWork, TestUnitOfWork>());

        act.Should().Throw<ArgumentException>()
            .WithMessage("*is not an interface type*");
    }

    [Fact]
    public void UseUnitOfWork_IdempotentDoubleCall_DoesNotDuplicateDescriptors()
    {
        var services = new ServiceCollection();

        services.AddRapidRepo(o => o.UseUnitOfWork<ITestUnitOfWork, TestUnitOfWork>());
        services.AddRapidRepo(o => o.UseUnitOfWork<ITestUnitOfWork, TestUnitOfWork>());

        services.Count(d => d.ServiceType == typeof(ITestUnitOfWork))
            .Should().Be(1);
    }

    [Fact]
    public void UseUnitOfWork_SingleType_AutoDetectsInterface()
    {
        var services = new ServiceCollection();

        services.AddRapidRepo(o => o.UseUnitOfWork<TestUnitOfWork>());

        services.Should().Contain(d =>
            d.ServiceType == typeof(ITestUnitOfWork) &&
            d.ImplementationType == typeof(TestUnitOfWork));
    }

    [Fact]
    public void UseUnitOfWork_SingleType_Throws_WhenNoUserDefinedInterface()
    {
        var services = new ServiceCollection();

        var act = () => services.AddRapidRepo(o => o.UseUnitOfWork<DirectUnitOfWork>());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*does not implement any user-defined IUnitOfWork*");
    }

    [Fact]
    public void UseUnitOfWork_SingleType_Throws_WhenMultipleUserDefinedInterfaces()
    {
        var services = new ServiceCollection();

        var act = () => services.AddRapidRepo(o => o.UseUnitOfWork<MultiInterfaceUnitOfWork>());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*multiple user-defined IUnitOfWork*");
    }

    [Fact]
    public void UseUnitOfWork_SingleType_SameImplTwice_Throws()
    {
        var services = new ServiceCollection();

        var act = () => services.AddRapidRepo(o =>
        {
            o.UseUnitOfWork<TestUnitOfWork>();
            o.UseUnitOfWork<TestUnitOfWork>();
        });

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void UseUnitOfWork_SingleType_MultipleInOneCall_AllRegistered()
    {
        // Verifies the common multi-DbContext scenario:
        // two distinct UoW implementations, each with its own interface,
        // registered in a single AddRapidRepo call.
        var services = new ServiceCollection();

        services.AddRapidRepo(o =>
        {
            o.UseUnitOfWork<TestUnitOfWork>();   // auto-detects ITestUnitOfWork  (Guid key)
            o.UseUnitOfWork<SecondUnitOfWork>(); // auto-detects ISecondUnitOfWork (int key)
        });

        services.Should().Contain(d =>
            d.ServiceType == typeof(ITestUnitOfWork) &&
            d.ImplementationType == typeof(TestUnitOfWork));

        services.Should().Contain(d =>
            d.ServiceType == typeof(ISecondUnitOfWork) &&
            d.ImplementationType == typeof(SecondUnitOfWork));
    }

    [Fact]
    public void UseUnitOfWork_SingleType_Throws_ForAbstractClass()
    {
        var services = new ServiceCollection();

        var act = () => services.AddRapidRepo(o => o.UseUnitOfWork<AbstractUoW>());

        act.Should().Throw<ArgumentException>()
            .WithMessage("*abstract*");
    }

    [Fact]
    public void UseUnitOfWork_SingleType_CrossOverload_DuplicateInterface_Throws()
    {
        // Two-type overload registers ITestUnitOfWork first;
        // single-type overload detects the same interface and must throw.
        var services = new ServiceCollection();

        var act = () => services.AddRapidRepo(o =>
        {
            o.UseUnitOfWork<ITestUnitOfWork, TestUnitOfWork>();
            o.UseUnitOfWork<TestUnitOfWork>();
        });

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already been registered*");
    }

    [Fact]
    public void UseUnitOfWork_SingleType_RespectsConfiguredLifetime()
    {
        var services = new ServiceCollection();

        services.AddRapidRepo(o =>
        {
            o.Lifetime = ServiceLifetime.Transient;
            o.UseUnitOfWork<TestUnitOfWork>();
        });

        services.Should().Contain(d =>
            d.ServiceType == typeof(ITestUnitOfWork) &&
            d.Lifetime == ServiceLifetime.Transient);
    }

    [Fact]
    public void UseUnitOfWork_SingleType_SameTUserKey_BothInterfacesRegistered_ForwardingAliasFirstOneWins()
    {
        // TestUnitOfWork (Guid) and ThirdUnitOfWork (Guid) share the same TUserKey.
        // Both user-defined interfaces must be registered.
        // The IUnitOfWork<Guid> forwarding alias must appear exactly once (TryAdd — first wins).
        var services = new ServiceCollection();

        services.AddRapidRepo(o =>
        {
            o.UseUnitOfWork<TestUnitOfWork>();   // ITestUnitOfWork + IUnitOfWork<Guid>
            o.UseUnitOfWork<ThirdUnitOfWork>();  // IThirdUoW      + IUnitOfWork<Guid> (skipped by TryAdd)
        });

        services.Should().Contain(d =>
            d.ServiceType == typeof(ITestUnitOfWork) &&
            d.ImplementationType == typeof(TestUnitOfWork));

        services.Should().Contain(d =>
            d.ServiceType == typeof(IThirdUoW) &&
            d.ImplementationType == typeof(ThirdUnitOfWork));

        services.Count(d => d.ServiceType == typeof(IUnitOfWork<Guid>))
            .Should().Be(1);
    }

    // ── IncludeNamespace / ExcludeNamespace null guard ─────────────────────────

    [Fact]
    public void IncludeNamespace_Null_Throws_ArgumentNullException()
    {
        var act = () => new RapidRepoOptions().IncludeNamespace(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ExcludeNamespace_Null_Throws_ArgumentNullException()
    {
        var act = () => new RapidRepoOptions().ExcludeNamespace(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // ── Filter precedence (exclude wins) ───────────────────────────────────────

    [Fact]
    public void ExcludeNamespace_BeatsIncludeNamespace_WhenNamespaceOverlaps()
    {
        var services = new ServiceCollection();

        services.AddRapidRepo(o =>
        {
            o.ScanAssembliesContaining<WidgetRepository>();
            // Include the broad TestData namespace, then exclude the Ambiguous sub-namespace.
            o.IncludeNamespace("RapidRepo.Extensions.DependencyInjection.Tests.TestData");
            o.ExcludeNamespace("RapidRepo.Extensions.DependencyInjection.Tests.TestData.Ambiguous");
        });

        // Types in TestData (not Ambiguous) are registered.
        services.Should().Contain(d => d.ImplementationType == typeof(WidgetRepository));
        services.Should().Contain(d => d.ImplementationType == typeof(GadgetRepository));

        // Types in TestData.Ambiguous are excluded despite the broad include.
        services.Should().NotContain(d => d.ImplementationType == typeof(GizmoRepositoryA));
        services.Should().NotContain(d => d.ImplementationType == typeof(GizmoRepositoryB));
    }

    [Fact]
    public void ExcludeType_BeatsIncludeType_ForSameType()
    {
        var services = new ServiceCollection();

        services.AddRapidRepo(o =>
        {
            o.ScanAssembliesContaining<WidgetRepository>();
            o.Exclude(ExcludeAmbiguous);
            o.IncludeType<WidgetRepository>();
            o.ExcludeType<WidgetRepository>(); // exclude must win
        });

        services.Should().NotContain(d => d.ImplementationType == typeof(WidgetRepository));
    }

    // ── RegisterGenericRepositories ────────────────────────────────────────────

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

        services.AddRapidRepo(o => o.RegisterGenericRepositories = true);

        services.Should().Contain(d =>
            d.ServiceType == typeof(IRepository<,>) &&
            d.ImplementationType == typeof(Repository<,>));
    }

    [Fact]
    public void RegisterGenericRepositories_True_RegistersIReadOnlyRepositoryOpenGeneric()
    {
        var services = new ServiceCollection();

        services.AddRapidRepo(o => o.RegisterGenericRepositories = true);

        services.Should().Contain(d =>
            d.ServiceType == typeof(IReadOnlyRepository<,>) &&
            d.ImplementationType == typeof(Repository<,>));
    }

    [Fact]
    public void RegisterGenericRepositories_True_RegistersIWriteRepositoryOpenGeneric()
    {
        var services = new ServiceCollection();

        services.AddRapidRepo(o => o.RegisterGenericRepositories = true);

        services.Should().Contain(d =>
            d.ServiceType == typeof(IWriteRepository<,>) &&
            d.ImplementationType == typeof(Repository<,>));
    }

    [Fact]
    public void RegisterGenericRepositories_True_RespectsConfiguredLifetime()
    {
        var services = new ServiceCollection();

        services.AddRapidRepo(o =>
        {
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

        services.AddRapidRepo(o => o.RegisterGenericRepositories = true);
        services.AddRapidRepo(o => o.RegisterGenericRepositories = true);

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
            d.ImplementationType == typeof(Repository<,>));
    }

    // ── ExcludeNamespace / IncludeNamespace ────────────────────────────────────

    [Fact]
    public void ExcludeNamespace_RemovesTypesInMatchingNamespace()
    {
        var services = new ServiceCollection();

        services.AddRapidRepo(o =>
        {
            o.ScanAssembliesContaining<WidgetRepository>();
            o.ExcludeNamespace("RapidRepo.Extensions.DependencyInjection.Tests.TestData.Ambiguous");
        });

        services.Should().NotContain(d => d.ImplementationType == typeof(GizmoRepositoryA));
        services.Should().NotContain(d => d.ImplementationType == typeof(GizmoRepositoryB));
    }

    [Fact]
    public void ExcludeNamespace_DoesNotAffectTypesOutsideNamespace()
    {
        var services = new ServiceCollection();

        services.AddRapidRepo(o =>
        {
            o.ScanAssembliesContaining<WidgetRepository>();
            o.ExcludeNamespace("RapidRepo.Extensions.DependencyInjection.Tests.TestData.Ambiguous");
        });

        services.Should().Contain(d => d.ImplementationType == typeof(WidgetRepository));
        services.Should().Contain(d => d.ImplementationType == typeof(GadgetRepository));
    }

    [Fact]
    public void IncludeNamespace_OnlyRegistersTypesInMatchingNamespace()
    {
        var services = new ServiceCollection();

        services.AddRapidRepo(o =>
        {
            o.ScanAssembliesContaining<WidgetRepository>();
            o.IncludeNamespace("RapidRepo.Extensions.DependencyInjection.Tests.TestData.Ambiguous");
            o.ThrowOnAmbiguousRegistration = false; // Ambiguous ns intentionally has two IGizmoRepository impls
        });

        // At least one Ambiguous-namespace type was discovered
        services.Should().Contain(d => d.ImplementationType == typeof(GizmoRepositoryA));
        // Types outside the included namespace were not registered
        services.Should().NotContain(d => d.ImplementationType == typeof(WidgetRepository));
        services.Should().NotContain(d => d.ImplementationType == typeof(GadgetRepository));
    }

    // ── ExcludeType / IncludeType ──────────────────────────────────────────────

    [Fact]
    public void ExcludeType_RemovesSpecificConcreteType()
    {
        var services = new ServiceCollection();

        services.AddRapidRepo(o =>
        {
            o.ScanAssembliesContaining<WidgetRepository>();
            o.Exclude(ExcludeAmbiguous);
            o.ExcludeType<WidgetRepository>();
        });

        services.Should().NotContain(d => d.ImplementationType == typeof(WidgetRepository));
        services.Should().Contain(d => d.ImplementationType == typeof(GadgetRepository));
    }

    [Fact]
    public void IncludeType_OnlyRegistersSpecificConcreteType()
    {
        var services = new ServiceCollection();

        services.AddRapidRepo(o =>
        {
            o.ScanAssembliesContaining<WidgetRepository>();
            o.IncludeType<GadgetRepository>();
        });

        services.Should().Contain(d => d.ImplementationType == typeof(GadgetRepository));
        services.Should().NotContain(d => d.ImplementationType == typeof(WidgetRepository));
    }
}
