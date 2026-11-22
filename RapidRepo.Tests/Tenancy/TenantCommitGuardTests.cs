using AwesomeAssertions;
using RapidRepo.Auditing;
using RapidRepo.Entities;
using RapidRepo.Entities.Interfaces;
using RapidRepo.Repositories.Interfaces;
using RapidRepo.Tenancy;
using RapidRepo.UnitOfWork;

namespace RapidRepo.Tests.Tenancy;

/// <summary>
/// The provider-neutral tenant rules a commit applies, and where the unit of work gets the current tenant from.
/// </summary>
public class TenantCommitGuardTests
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();

    [Fact]
    public void ApplyToAdded_StampsTheCurrentTenant_WhenUnset()
    {
        var entity = new Invoice();

        Guard(TenantA).ApplyToAdded(entity);

        entity.TenantId.Should().Be(TenantA);
    }

    [Fact]
    public void ApplyToAdded_AcceptsTheCurrentTenant_AndRejectsAnother()
    {
        Guard(TenantA).ApplyToAdded(new Invoice { TenantId = TenantA });

        var act = () => Guard(TenantA).ApplyToAdded(new Invoice { TenantId = TenantB });

        act.Should().Throw<TenantViolationException>().Which.EntityType.Should().Be<Invoice>();
    }

    [Fact]
    public void ApplyToAdded_WithoutATenant_Throws()
    {
        var act = () => Guard(null).ApplyToAdded(new Invoice());

        act.Should().Throw<TenantViolationException>().WithMessage("*no current tenant*");
    }

    [Fact]
    public void ApplyToAdded_InAHostScope_RequiresAnExplicitTenant()
    {
        var host = new TenantCommitGuard(isHostScope: true, _ => null);

        host.ApplyToAdded(new Invoice { TenantId = TenantB });
        var act = () => host.ApplyToAdded(new Invoice());

        act.Should().Throw<TenantViolationException>().WithMessage("*host scope*");
    }

    [Fact]
    public void ApplyToAdded_RejectsAnEmptyStringTenantId()
    {
        var act = () => new TenantCommitGuard(false, _ => "acme").ApplyToAdded(new Document { TenantId = "" });

        act.Should().Throw<TenantViolationException>().WithMessage("*empty string*");
    }

    [Fact]
    public void VerifyExisting_RejectsARowStoredForAnotherTenant_WhateverTheInstanceSays()
    {
        var act = () => Guard(TenantA).VerifyExisting(
            new Invoice { TenantId = TenantA }, TenantWriteKind.Update, storedTenantId: TenantB);

        act.Should().Throw<TenantViolationException>().WithMessage("*belongs to another tenant*");
    }

    [Fact]
    public void VerifyExisting_RejectsAChangedTenantId_OnUpdate_ButNotOnDelete()
    {
        var moved = new Invoice { TenantId = TenantB };

        var update = () => Guard(TenantA).VerifyExisting(moved, TenantWriteKind.Update, storedTenantId: TenantA);
        var delete = () => Guard(TenantA).VerifyExisting(moved, TenantWriteKind.Delete, storedTenantId: TenantA);

        update.Should().Throw<TenantViolationException>().WithMessage("*cannot move between tenants*");
        delete.Should().NotThrow();
    }

    [Fact]
    public void VerifyExisting_LetsAMissingRowThrough()
    {
        var act = () => Guard(TenantA).VerifyExisting(
            new Invoice { TenantId = TenantA }, TenantWriteKind.Update);

        act.Should().NotThrow();
    }

    [Fact]
    public void VerifyExisting_IsSkippedInAHostScope()
    {
        var act = () => new TenantCommitGuard(isHostScope: true, _ => null).VerifyExisting(
            new Invoice { TenantId = TenantB }, TenantWriteKind.Update, storedTenantId: TenantA);

        act.Should().NotThrow();
    }

    [Fact]
    public void VerifyReference_AcceptsARowOfTheCurrentTenant_AndRejectsAnother()
    {
        Guard(TenantA).VerifyReference(new Invoice(), typeof(Invoice), ["ParentId"], TenantA);

        var act = () => Guard(TenantA).VerifyReference(new Invoice(), typeof(Invoice), ["ParentId"], TenantB);

        act.Should().Throw<TenantViolationException>()
            .WithMessage("*ParentId refers to a Invoice that the current tenant cannot see*")
            .Which.EntityType.Should().Be<Invoice>();
    }

    [Fact]
    public void VerifyReference_FromANonTenantEntity_IsCheckedAgainstTheCurrentTenant()
    {
        var act = () => Guard(TenantA).VerifyReference(new object(), typeof(Invoice), ["InvoiceId"], TenantB);

        act.Should().Throw<TenantViolationException>().Which.EntityType.Should().Be<object>();
    }

    [Fact]
    public void VerifyReference_WithoutATenant_Throws()
    {
        var act = () => Guard(null).VerifyReference(new Invoice(), typeof(Invoice), ["ParentId"], TenantA);

        act.Should().Throw<TenantViolationException>().WithMessage("*no current tenant*");
    }

    [Fact]
    public void VerifyReference_IsSkippedInAHostScope()
    {
        var act = () => new TenantCommitGuard(isHostScope: true, _ => null)
            .VerifyReference(new Invoice(), typeof(Invoice), ["ParentId"], TenantB);

        act.Should().NotThrow();
    }

    [Fact]
    public void TheTenant_IsResolvedOncePerKeyType()
    {
        var calls = 0;
        var guard = new TenantCommitGuard(false, _ => { calls++; return TenantA; });

        guard.ApplyToAdded(new Invoice());
        guard.ApplyToAdded(new Invoice());
        guard.VerifyExisting(new Invoice { TenantId = TenantA }, TenantWriteKind.Update, TenantA);

        calls.Should().Be(1);
    }

    [Fact]
    public void CreateTenantGuard_AsksTheRegisteredProvider_AndTreatsTheDefaultValueAsNone()
    {
        var withTenant = new GuardingUnitOfWork(new Services(new FixedTenant(TenantA)));
        var withoutTenant = new GuardingUnitOfWork(new Services(new FixedTenant(Guid.Empty)));
        var withoutProvider = new GuardingUnitOfWork(serviceProvider: null);

        withTenant.Guard().RequireCurrentTenant(typeof(Invoice)).Should().Be(TenantA);
        ((Action)(() => withoutTenant.Guard().RequireCurrentTenant(typeof(Invoice)))).Should().Throw<TenantViolationException>();
        ((Action)(() => withoutProvider.Guard().RequireCurrentTenant(typeof(Invoice)))).Should().Throw<TenantViolationException>();
    }

    [Fact]
    public void CreateTenantGuard_PrefersAnOverride_OverTheProvider()
    {
        var uow = new GuardingUnitOfWork(new Services(new FixedTenant(TenantA)));

        using (new TenantScope<Guid>().Override(TenantB))
            uow.Guard().RequireCurrentTenant(typeof(Invoice)).Should().Be(TenantB);
    }

    [Fact]
    public void TenantTraits_RejectsAnEntityWithTwoTenantKeys()
    {
        var act = () => TenantTraits.GetTenantKeyType(typeof(TwoTenants));

        act.Should().Throw<InvalidOperationException>().WithMessage("*more than once*");
    }

    private static TenantCommitGuard Guard(Guid? tenant) => new(isHostScope: false, _ => tenant);

    private sealed class Invoice : BaseEntity<int>, ITenantEntity<Guid>
    {
        public Guid TenantId { get; set; }
    }

    private sealed class Document : BaseEntity<int>, ITenantEntity<string>
    {
        public string TenantId { get; set; } = null!;
    }

    private sealed class TwoTenants : ITenantEntity<Guid>, ITenantEntity<int>
    {
        Guid ITenantEntity<Guid>.TenantId { get; set; }

        int ITenantEntity<int>.TenantId { get; set; }
    }

    private sealed class FixedTenant(Guid tenant) : ICurrentTenantProvider<Guid>
    {
        public Guid GetCurrentTenantId() => tenant;
    }

    private sealed class Services(ICurrentTenantProvider<Guid> provider) : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(ICurrentTenantProvider<Guid>) ? provider : null;
    }

    private sealed class GuardingUnitOfWork(IServiceProvider? serviceProvider)
        : UnitOfWorkBase<Guid>(Guid.Empty, serviceProvider)
    {
        public TenantCommitGuard Guard() => CreateTenantGuard();

        protected override IRepository<TEntity, TKey> CreateRepository<TEntity, TKey>() => throw new NotSupportedException();

        protected override void CommitCore(Guid? userId) => throw new NotSupportedException();

        protected override Task CommitCoreAsync(Guid? userId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
