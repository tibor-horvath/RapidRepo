using AwesomeAssertions;
using RapidRepo.Repositories;
using RapidRepo.Tenancy;
using RapidRepo.Tenancy.Host;
using Xunit;

namespace RapidRepo.Testing.Conformance;

/// <summary>
/// Multi-tenancy: tenant entities are stamped, filtered and guarded, including against hostile writes that carry
/// another tenant's id. Opt-in: only providers with an <see cref="ITenantConformanceStore"/> run it.
/// </summary>
/// <typeparam name="TStore">The provider's store.</typeparam>
public abstract class TenantConformanceTests<TStore> : ConformanceTests<TStore>
    where TStore : ITenantConformanceStore, new()
{
    /// <summary>A tenant.</summary>
    protected static readonly Guid TenantA = new("00000000-0000-0000-0000-0000000000aa");

    /// <summary>Another tenant.</summary>
    protected static readonly Guid TenantB = new("00000000-0000-0000-0000-0000000000bb");

    private static readonly ReadOptions IncludeDeleted = new() { IncludeDeleted = true };

    private readonly ITenantScope<Guid> _tenantScope = new TenantScope<Guid>();
    private readonly ITenantHostScope _hostScope = new TenantHostScope();

    // ── Stamping ───────────────────────────────────────────────────────────────

    [Fact]
    public void Add_StampsTheCurrentTenant()
    {
        var id = SeedInvoice(TenantA, "mine");

        ReadAsHost(id)!.TenantId.Should().Be(TenantA);
    }

    [Fact]
    public void Add_ForAnotherTenant_Throws_AndWritesNothing()
    {
        var session = As(TenantA);
        session.Invoices().Add(new Invoice { Title = "planted", TenantId = TenantB });

        var commit = () => session.UnitOfWork.Commit();

        commit.Should().Throw<TenantViolationException>();
        CountAsHost().Should().Be(0);
    }

    [Fact]
    public void Add_ForTheCurrentTenant_Explicitly_Succeeds()
    {
        var session = As(TenantA);
        session.Invoices().Add(new Invoice { TenantId = TenantA });

        session.UnitOfWork.Commit();

        CountAsHost().Should().Be(1);
    }

    [Fact]
    public void Commit_WithoutATenant_Throws_AndWritesNothing()
    {
        var session = WithoutTenant();
        session.Invoices().Add(new Invoice());

        var commit = () => session.UnitOfWork.Commit();

        commit.Should().Throw<TenantViolationException>();
        CountAsHost().Should().Be(0);
    }

    [Fact]
    public void Commit_WithoutATenant_OfNonTenantEntities_Succeeds()
    {
        var session = WithoutTenant();
        session.Widgets().Add(new Widget { Name = "shared" });

        session.UnitOfWork.Commit();

        InNewSession(s => s.Widgets().Count()).Should().Be(1);
    }

    // ── Reads ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Reads_ReturnOnlyTheCurrentTenantsRows()
    {
        SeedInvoice(TenantA, "a1");
        SeedInvoice(TenantA, "a2");
        var theirs = SeedInvoice(TenantB, "b1");

        var session = As(TenantA);

        session.Invoices().GetAll().Select(i => i.Title).Should().BeEquivalentTo(["a1", "a2"]);
        session.Invoices().Count().Should().Be(2);
        session.Invoices().GetById(theirs).Should().BeNull();
        session.Invoices().ExistsById(theirs).Should().BeFalse();
        session.Invoices().Any(i => i.Title == "b1").Should().BeFalse();
    }

    [Fact]
    public void Read_WithoutATenant_Throws()
    {
        SeedInvoice(TenantA);

        var read = () => WithoutTenant().Invoices().GetAll();

        read.Should().Throw<TenantViolationException>();
    }

    [Fact]
    public void IncludeDeleted_KeepsTheTenantFilter()
    {
        SeedInvoice(TenantA, "mine, deleted", deleted: true);
        SeedInvoice(TenantB, "theirs, deleted", deleted: true);

        As(TenantA).Invoices().GetAll(options: IncludeDeleted).Select(i => i.Title)
            .Should().BeEquivalentTo(["mine, deleted"]);
    }

    // ── Hostile writes ────────────────────────────────────────────────────────

    [Fact]
    public void Update_OfAnUntrackedInstance_CarryingAnotherTenantsId_Throws_AndLeavesTheRowUnchanged()
    {
        var theirs = SeedInvoice(TenantB, "theirs");
        var session = As(TenantA);

        // A request body: another tenant's id, with the caller's own TenantId.
        session.Invoices().Update(new Invoice { Id = theirs, TenantId = TenantA, Title = "hijacked" });
        var commit = () => session.UnitOfWork.Commit();

        commit.Should().Throw<TenantViolationException>();
        var row = ReadAsHost(theirs)!;
        row.Title.Should().Be("theirs");
        row.TenantId.Should().Be(TenantB);
    }

    [Fact]
    public async Task UpdateAsync_OfAnUntrackedInstance_CarryingAnotherTenantsId_Throws()
    {
        var theirs = SeedInvoice(TenantB, "theirs");
        var session = As(TenantA);

        session.Invoices().Update(new Invoice { Id = theirs, TenantId = TenantA, Title = "hijacked" });
        var commit = () => session.UnitOfWork.CommitAsync();

        await commit.Should().ThrowAsync<TenantViolationException>();
        ReadAsHost(theirs)!.Title.Should().Be("theirs");
    }

    [Fact]
    public void HardDelete_OfAnUntrackedInstance_OfAnotherTenantsRow_Throws_AndKeepsTheRow()
    {
        var theirs = SeedInvoice(TenantB);
        var session = As(TenantA);

        session.Invoices().HardDelete(new Invoice { Id = theirs, TenantId = TenantA });
        var commit = () => session.UnitOfWork.Commit();

        commit.Should().Throw<TenantViolationException>();
        ReadAsHost(theirs).Should().NotBeNull();
    }

    [Fact]
    public void Delete_OfAnUntrackedInstance_OfAnotherTenantsRow_Throws_AndKeepsItUndeleted()
    {
        var theirs = SeedInvoice(TenantB);
        var session = As(TenantA);

        session.Invoices().Delete(new Invoice { Id = theirs, TenantId = TenantA });
        var commit = () => session.UnitOfWork.Commit();

        commit.Should().Throw<TenantViolationException>();
        ReadAsHost(theirs)!.DeletedAt.Should().BeNull();
    }

    [Fact]
    public void ChangingTheTenantId_OfAnOwnEntity_Throws()
    {
        var mine = SeedInvoice(TenantA);
        var session = As(TenantA);
        var invoice = session.Invoices().GetById(mine)!;

        invoice.TenantId = TenantB;
        session.Invoices().Update(invoice);
        var commit = () => session.UnitOfWork.Commit();

        commit.Should().Throw<TenantViolationException>();
        ReadAsHost(mine)!.TenantId.Should().Be(TenantA);
    }

    [Fact]
    public void ByIdWrites_WithAnotherTenantsId_BehaveLikeAMissingId()
    {
        var theirs = SeedInvoice(TenantB);
        var theirsDeleted = SeedInvoice(TenantB, deleted: true);
        var session = As(TenantA);

        session.Invoices().DeleteById(theirs);
        session.Invoices().HardDeleteById(theirs, includeDeleted: true);
        session.Invoices().RestoreById(theirsDeleted);
        session.UnitOfWork.Commit();

        ReadAsHost(theirs)!.DeletedAt.Should().BeNull();
        ReadAsHost(theirsDeleted)!.DeletedAt.Should().NotBeNull();
    }

    // ── References ────────────────────────────────────────────────────────────

    [Fact]
    public void Add_ReferencingAnotherTenantsRow_Throws_AndWritesNothing()
    {
        var theirs = SeedInvoice(TenantB);
        var session = As(TenantA);
        session.Payments().Add(new Payment { InvoiceId = theirs, Amount = 10 });

        var commit = () => session.UnitOfWork.Commit();

        commit.Should().Throw<TenantViolationException>().Which.EntityType.Should().Be<Payment>();
        CountPaymentsAsHost().Should().Be(0);
    }

    [Fact]
    public async Task AddAsync_ReferencingAnotherTenantsDeletedRow_Throws()
    {
        var theirs = SeedInvoice(TenantB, deleted: true);
        var session = As(TenantA);
        await session.Payments().AddAsync(new Payment { InvoiceId = theirs });

        var commit = () => session.UnitOfWork.CommitAsync();

        await commit.Should().ThrowAsync<TenantViolationException>();
        CountPaymentsAsHost().Should().Be(0);
    }

    [Fact]
    public void Add_ReferencingAnOwnRow_Succeeds()
    {
        var mine = SeedInvoice(TenantA);
        var session = As(TenantA);
        session.Payments().Add(new Payment { InvoiceId = mine, Amount = 10 });

        session.UnitOfWork.Commit();

        CountPaymentsAsHost().Should().Be(1);
    }

    [Fact]
    public void Update_ChangingAReference_ToAnotherTenantsRow_Throws_AndLeavesItUnchanged()
    {
        var mine = SeedInvoice(TenantA);
        var theirs = SeedInvoice(TenantB);
        var paymentId = SeedPayment(TenantA, mine);

        var session = As(TenantA);
        var payment = session.Payments().GetById(paymentId)!;
        payment.InvoiceId = theirs;
        session.Payments().Update(payment);
        var commit = () => session.UnitOfWork.Commit();

        commit.Should().Throw<TenantViolationException>();
        ReadPaymentAsHost(paymentId)!.InvoiceId.Should().Be(mine);
    }

    [Fact]
    public void Update_OfAnUntrackedInstance_ReferencingAnotherTenantsRow_Throws()
    {
        var mine = SeedInvoice(TenantA);
        var theirs = SeedInvoice(TenantB);
        var paymentId = SeedPayment(TenantA, mine);

        var session = As(TenantA);
        session.Payments().Update(new Payment { Id = paymentId, TenantId = TenantA, InvoiceId = theirs });
        var commit = () => session.UnitOfWork.Commit();

        commit.Should().Throw<TenantViolationException>();
        ReadPaymentAsHost(paymentId)!.InvoiceId.Should().Be(mine);
    }

    [Fact]
    public void HostScope_MayReferAcrossTenants()
    {
        var theirs = SeedInvoice(TenantB);

        using (_hostScope.BeginHostScope())
        {
            var session = WithoutTenant();
            session.Payments().Add(new Payment { TenantId = TenantA, InvoiceId = theirs });
            session.UnitOfWork.Commit();
        }

        CountPaymentsAsHost().Should().Be(1);
    }

    // ── Own writes ────────────────────────────────────────────────────────────

    [Fact]
    public void Update_Delete_AndRestore_OfOwnRows_Succeed()
    {
        var mine = SeedInvoice(TenantA, "before");

        var update = As(TenantA);
        update.Invoices().Update(new Invoice { Id = mine, TenantId = TenantA, Title = "after" });
        update.UnitOfWork.Commit();

        var delete = As(TenantA);
        delete.Invoices().DeleteById(mine);
        delete.UnitOfWork.Commit();

        var restore = As(TenantA);
        restore.Invoices().RestoreById(mine);
        restore.UnitOfWork.Commit();

        var row = ReadAsHost(mine)!;
        row.Title.Should().Be("after");
        row.DeletedAt.Should().BeNull();
    }

    [Fact]
    public void HardDelete_OfAnOwnRow_Succeeds()
    {
        var mine = SeedInvoice(TenantA);
        var session = As(TenantA);

        session.Invoices().HardDeleteById(mine);
        session.UnitOfWork.Commit();

        ReadAsHost(mine).Should().BeNull();
    }

    // ── Scopes ────────────────────────────────────────────────────────────────

    [Fact]
    public void Override_ActsAsTheGivenTenant_ForReadsAndWrites()
    {
        SeedInvoice(TenantB, "theirs");

        using (_tenantScope.Override(TenantA))
        {
            var session = WithoutTenant();
            session.Invoices().Add(new Invoice { Title = "mine" });
            session.UnitOfWork.Commit();

            WithoutTenant().Invoices().GetAll().Select(i => i.Title).Should().BeEquivalentTo(["mine"]);
        }

        CountAsHost().Should().Be(2);
    }

    [Fact]
    public void Override_TakesPrecedenceOverTheProvider_AndNests()
    {
        SeedInvoice(TenantA, "a");
        SeedInvoice(TenantB, "b");

        using (_tenantScope.Override(TenantB))
        {
            As(TenantA).Invoices().GetAll().Select(i => i.Title).Should().BeEquivalentTo(["b"]);

            using (_tenantScope.Override(TenantA))
                As(TenantB).Invoices().GetAll().Select(i => i.Title).Should().BeEquivalentTo(["a"]);

            As(TenantA).Invoices().GetAll().Select(i => i.Title).Should().BeEquivalentTo(["b"]);
        }

        As(TenantA).Invoices().GetAll().Select(i => i.Title).Should().BeEquivalentTo(["a"]);
    }

    [Fact]
    public void HostScope_ReadsEveryTenant_AndWritesAcrossTenants()
    {
        SeedInvoice(TenantA, "a");
        var theirs = SeedInvoice(TenantB, "b");

        using (_hostScope.BeginHostScope())
        {
            WithoutTenant().Invoices().Count().Should().Be(2);

            var session = WithoutTenant();
            session.Invoices().Update(new Invoice { Id = theirs, TenantId = TenantB, Title = "fixed by host" });
            session.Invoices().Add(new Invoice { TenantId = TenantB, Title = "seeded" });
            session.UnitOfWork.Commit();
        }

        ReadAsHost(theirs)!.Title.Should().Be("fixed by host");
        CountAsHost().Should().Be(3);
    }

    [Fact]
    public void HostScope_RequiresAnExplicitTenantId_OnAdd()
    {
        using var host = _hostScope.BeginHostScope();
        var session = WithoutTenant();
        session.Invoices().Add(new Invoice { Title = "nobody's" });

        var commit = () => session.UnitOfWork.Commit();

        commit.Should().Throw<TenantViolationException>();
    }

    [Fact]
    public void Override_InsideAHostScope_ActsAsThatTenant()
    {
        SeedInvoice(TenantA, "a");
        SeedInvoice(TenantB, "b");

        using (_hostScope.BeginHostScope())
        using (_tenantScope.Override(TenantA))
            WithoutTenant().Invoices().GetAll().Select(i => i.Title).Should().BeEquivalentTo(["a"]);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>Opens a session whose provider returns <paramref name="tenantId"/>.</summary>
    protected IConformanceSession As(Guid tenantId) => Track(Store.OpenSession(Clock, new ConformanceTenant(tenantId)));

    /// <summary>Opens a session whose provider returns no tenant.</summary>
    protected IConformanceSession WithoutTenant() => Track(Store.OpenSession(Clock, new ConformanceTenant(null)));

    /// <summary>Adds an invoice as <paramref name="tenantId"/>, optionally soft-deleting it, and returns its id.</summary>
    protected int SeedInvoice(Guid tenantId, string title = "", bool deleted = false)
    {
        var invoice = new Invoice { Title = title };

        var session = As(tenantId);
        session.Invoices().Add(invoice);
        session.UnitOfWork.Commit();

        if (deleted)
        {
            var delete = As(tenantId);
            delete.Invoices().DeleteById(invoice.Id);
            delete.UnitOfWork.Commit();
        }

        return invoice.Id;
    }

    /// <summary>Reads an invoice of any tenant, deleted or not.</summary>
    protected Invoice? ReadAsHost(int id)
    {
        using var host = _hostScope.BeginHostScope();
        return WithoutTenant().Invoices().GetById(id, IncludeDeleted);
    }

    /// <summary>Adds a payment of <paramref name="invoiceId"/> as <paramref name="tenantId"/> and returns its id.</summary>
    protected int SeedPayment(Guid tenantId, int invoiceId)
    {
        var payment = new Payment { InvoiceId = invoiceId };

        var session = As(tenantId);
        session.Payments().Add(payment);
        session.UnitOfWork.Commit();

        return payment.Id;
    }

    /// <summary>Reads a payment of any tenant.</summary>
    protected Payment? ReadPaymentAsHost(int id)
    {
        using var host = _hostScope.BeginHostScope();
        return WithoutTenant().Payments().GetById(id);
    }

    /// <summary>Counts every tenant's payments.</summary>
    protected int CountPaymentsAsHost()
    {
        using var host = _hostScope.BeginHostScope();
        return WithoutTenant().Payments().Count();
    }

    /// <summary>Counts every tenant's invoices, deleted or not.</summary>
    protected int CountAsHost()
    {
        using var host = _hostScope.BeginHostScope();
        return WithoutTenant().Invoices().Count(options: IncludeDeleted);
    }
}
