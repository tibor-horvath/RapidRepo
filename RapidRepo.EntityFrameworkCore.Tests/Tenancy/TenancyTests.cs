using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using RapidRepo.Extensions;
using RapidRepo.Repositories;
using RapidRepo.Tenancy;
using RapidRepo.Tenancy.Host;

namespace RapidRepo.EntityFrameworkCore.Tests.Tenancy;

/// <summary>
/// EF Core specifics of multi-tenancy that the provider-neutral conformance suite does not reach: the validation,
/// graphs, bulk operations, composite keys, string keys and lifting filters.
/// </summary>
public sealed class TenancyTests : IDisposable
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();

    private readonly SqliteDatabase _database = new();
    private readonly SqliteDatabase _documents = new();
    private readonly TestTenant _tenant = new();
    private readonly ITenantHostScope _hostScope = new TenantHostScope();

    public TenancyTests()
    {
        using var context = NewContext();
        context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _database.Dispose();
        _documents.Dispose();
    }

    // ── Validation ─────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_TheDocumentedSetup_IsValid_AndWarnsAboutTheForeignKeyWithoutTenantId()
    {
        using var context = NewContext();

        var result = TenancyValidator.Validate(context);

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().ContainSingle().Which.Should().Contain("OrderLine").And.Contain("(TenantId, Id)");
    }

    [Fact]
    public void Validate_WithRequireTenantScopedForeignKeys_FailsOnTheForeignKey()
    {
        using var context = new StrictForeignKeyContext(_database.Options);

        var result = TenancyValidator.Validate(context);

        result.Errors.Should().ContainSingle().Which.Should().Contain("does not include TenantId");
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WithoutTheTenantFilter_FailsForEveryTenantEntity_InOneException()
    {
        using var context = new MissingFilterContext(_database.Options);

        var result = TenancyValidator.Validate(context);

        result.Errors.Should().HaveCount(3);
        result.Errors.Should().AllSatisfy(e => e.Should().Contain("AddTenantQueryFilters(this)"));
        var act = result.ThrowIfInvalid;
        var exception = act.Should().Throw<RapidRepoConfigurationException>().WithMessage("*3 problems*").Which;
        exception.Problems.Should().Equal(result.Errors);
        exception.Message.Should().Contain("- Order ").And.Contain("- OrderLine ").And.Contain("- Membership ");
    }

    [Fact]
    public void Read_OnAContextWithoutTheTenantFilter_Throws_InsteadOfReturningEveryTenantsRows()
    {
        using var context = new MissingFilterContext(_database.Options);

        var read = () => new EfRepository<Order, int>(context).GetAll();

        read.Should().Throw<InvalidOperationException>().WithMessage("*without the tenant query filter*");
    }

    [Fact]
    public void Validate_AContextThatCannotSupplyTheTenant_Fails()
    {
        using var context = new NoTenantSourceContext(_database.Options);

        TenancyValidator.Validate(context).Errors.Should().Contain(e => e.Contains("does not implement ITenantDbContext<Guid>"));
    }

    [Fact]
    public void AddTenantQueryFilters_RefusesToReplaceAHandWrittenAnonymousFilter()
    {
        using var context = new HandWrittenFilterContext(_database.Options);

        var build = () => context.Model;

        build.Should().Throw<InvalidOperationException>().WithMessage("*anonymous query filter*Migrating a hand-written tenant filter*");
    }

    [Fact]
    public void Validate_AContextWithoutTenantEntities_IsValid()
    {
        using var context = new PlainContext(new DbContextOptionsBuilder<PlainContext>().UseSqlite(_database.Connection).Options);

        var result = TenancyValidator.Validate(context);

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void AddTenantQueryFilters_RegistersANamedFilter_NextToTheSoftDeleteFilter()
    {
        using var context = NewContext();

        context.Model.FindEntityType(typeof(Order))!.GetDeclaredQueryFilters().Select(f => f.Key)
            .Should().BeEquivalentTo([ModelBuilderExtensions.SoftDeleteFilterKey, ModelBuilderExtensions.TenantFilterKey]);
    }

    // ── Graphs ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Add_StampsChildrenReachedThroughANavigation()
    {
        var order = new Order { Title = "with lines", Lines = [new OrderLine { Text = "one" }, new OrderLine { Text = "two" }] };

        Commit(TenantA, context => new EfRepository<Order, int>(context).Add(order));

        order.Lines.Should().AllSatisfy(l => l.TenantId.Should().Be(TenantA));
    }

    [Fact]
    public void Update_OfAGraph_ReachingAnotherTenantsChild_Throws_AndLeavesTheChildUnchanged()
    {
        var mine = Seed(TenantA, new Order { Title = "mine" });
        var theirs = Seed(TenantB, new Order { Title = "theirs", Lines = [new OrderLine { Text = "their line" }] });
        var theirLine = theirs.Lines[0].Id;

        var commit = () => Commit(TenantA, context => new EfRepository<Order, int>(context).Update(new Order
        {
            Id = mine.Id,
            TenantId = TenantA,
            Title = "mine",
            Lines = [new OrderLine { Id = theirLine, TenantId = TenantA, OrderId = mine.Id, Text = "stolen" }],
        }));

        commit.Should().Throw<TenantViolationException>();
        using var host = _hostScope.BeginHostScope();
        using var read = NewContext();
        var line = read.OrderLines.Single(l => l.Id == theirLine);
        line.Text.Should().Be("their line");
        line.OrderId.Should().Be(theirs.Id);
    }

    // ── References ────────────────────────────────────────────────────────────

    [Fact]
    public void Add_OfAChild_ReferencingAnotherTenantsParent_Throws_AndWritesNothing()
    {
        var theirs = Seed(TenantB, new Order { Title = "theirs" });

        var commit = () => Commit(TenantA, context => context.OrderLines.Add(new OrderLine { OrderId = theirs.Id, Text = "planted" }));

        commit.Should().Throw<TenantViolationException>().WithMessage("*OrderId refers to a Order*");
        using var host = _hostScope.BeginHostScope();
        using var read = NewContext();
        read.OrderLines.Should().BeEmpty();
    }

    [Fact]
    public void Add_OfALinkRow_ThatIsNotATenantEntity_ReferencingAnotherTenantsRow_Throws()
    {
        var theirs = Seed(TenantB, new Order { Title = "theirs" });

        var commit = () => Commit(TenantA, context => context.OrderNotes.Add(new OrderNote { OrderId = theirs.Id }));

        commit.Should().Throw<TenantViolationException>().Which.EntityType.Should().Be<OrderNote>();
    }

    [Fact]
    public void Add_OfALinkRow_ReferencingATenantRow_WithoutATenant_Throws()
    {
        var mine = Seed(TenantA, new Order { Title = "mine" });

        var commit = () => Commit(Guid.Empty, context => context.OrderNotes.Add(new OrderNote { OrderId = mine.Id }));

        commit.Should().Throw<TenantViolationException>().WithMessage("*no current tenant*");
    }

    [Fact]
    public void Add_OfALinkRow_ReferencingAnOwnRow_Succeeds()
    {
        var mine = Seed(TenantA, new Order { Title = "mine" });

        Commit(TenantA, context => context.OrderNotes.Add(new OrderNote { OrderId = mine.Id }));

        using var read = NewContext();
        read.OrderNotes.Should().ContainSingle();
    }

    [Fact]
    public void Update_ThatMovesAChild_UnderAnotherTenantsParent_Throws()
    {
        var mine = Seed(TenantA, new Order { Title = "mine", Lines = [new OrderLine { Text = "line" }] });
        var theirs = Seed(TenantB, new Order { Title = "theirs" });

        var commit = () => Commit(TenantA, context => context.OrderLines.Single().OrderId = theirs.Id);

        commit.Should().Throw<TenantViolationException>();
        using var read = NewContext();
        read.OrderLines.Single().OrderId.Should().Be(mine.Id);
    }

    // ── Composite keys ────────────────────────────────────────────────────────

    [Fact]
    public void Update_OfAnotherTenantsRow_WithACompositeKey_Throws()
    {
        Commit(TenantB, context => context.Add(new Membership { OrgId = 1, UserId = 7, Role = "member" }));
        Commit(TenantA, context => context.Add(new Membership { OrgId = 1, UserId = 8, Role = "member" }));

        var hijack = () => Commit(TenantA, context =>
        {
            context.Update(new Membership { OrgId = 1, UserId = 8, TenantId = TenantA, Role = "admin" });
            context.Update(new Membership { OrgId = 1, UserId = 7, TenantId = TenantA, Role = "admin" });
        });

        hijack.Should().Throw<TenantViolationException>();
        using var host = _hostScope.BeginHostScope();
        using var read = NewContext();
        read.Memberships.Select(m => m.Role).Should().AllBe("member");
    }

    // ── Lifting filters ───────────────────────────────────────────────────────

    [Fact]
    public void IgnoreAllQueryFilters_OnATenantEntity_Throws_OutsideAHostScope()
    {
        Seed(TenantA, new Order());
        _tenant.Current = TenantA;
        using var context = NewContext();
        var options = new EfReadOptions<Order> { IgnoreAllQueryFilters = true };

        var read = () => new EfRepository<Order, int>(context).GetAll(options: options);

        read.Should().Throw<TenantViolationException>().WithMessage("*IgnoreAllQueryFilters*");
    }

    [Fact]
    public void IgnoreAllQueryFilters_OnATenantEntity_ReadsEveryTenant_InsideAHostScope()
    {
        Seed(TenantA, new Order());
        Seed(TenantB, new Order { DeletedAt = DateTime.UtcNow });
        using var context = NewContext();

        using var host = _hostScope.BeginHostScope();

        new EfRepository<Order, int>(context).GetAll(options: new EfReadOptions<Order> { IgnoreAllQueryFilters = true })
            .Should().HaveCount(2);
    }

    [Fact]
    public void IgnoreAllQueryFilters_OnANonTenantEntity_IsUnaffected()
    {
        using (var seed = NewContext())
        {
            seed.Products.AddRange(new Product { Name = "hidden" }, new Product { Name = "shown" });
            seed.SaveChanges();
        }

        using var context = NewContext();

        new EfRepository<Product, int>(context).GetAll(options: new EfReadOptions<Product> { IgnoreAllQueryFilters = true })
            .Should().HaveCount(2);
    }

    [Fact]
    public void IncludeDeleted_OnANonTenantEntityWithAnAnonymousFilter_KeepsThe8xFallback()
    {
        using (var seed = NewContext())
        {
            seed.Products.AddRange(new Product { Name = "hidden" }, new Product { Name = "gone", DeletedAt = DateTime.UtcNow });
            seed.SaveChanges();
        }

        using var context = NewContext();

        new EfRepository<Product, int>(context).GetAll(options: new ReadOptions { IncludeDeleted = true }).Should().HaveCount(2);
    }

    // ── Bulk operations ───────────────────────────────────────────────────────

    [Fact]
    public void ExecuteUpdate_AndExecuteDelete_StayScopedToTheCurrentTenant()
    {
        Seed(TenantA, new Order { Title = "a" });
        Seed(TenantB, new Order { Title = "b" });
        _tenant.Current = TenantA;

        using (var context = NewContext())
        {
            context.Orders.ExecuteUpdate(s => s.SetProperty(o => o.Title, "renamed")).Should().Be(1);
            context.Orders.Where(o => o.Title == "b").ExecuteDelete().Should().Be(0);
        }

        using var host = _hostScope.BeginHostScope();
        using var read = NewContext();
        read.Orders.OrderBy(o => o.Title).Select(o => o.Title).Should().Equal("b", "renamed");
    }

    // ── String keys ───────────────────────────────────────────────────────────

    [Fact]
    public void StringKeys_AreStampedAndFiltered()
    {
        var current = "acme";
        using (var context = new DocumentContext(_documents.Options, () => current))
        {
            context.Database.EnsureCreated();
            context.Documents.Add(new Document { Title = "acme's" });
            new TestUnitOfWork(context).Commit();
        }

        current = "globex";
        using (var context = new DocumentContext(_documents.Options, () => current))
        {
            new EfRepository<Document, int>(context).GetAll().Should().BeEmpty();
        }

        current = "acme";
        using (var context = new DocumentContext(_documents.Options, () => current))
        {
            new EfRepository<Document, int>(context).GetAll().Single().TenantId.Should().Be("acme");
        }
    }

    [Fact]
    public void AnEmptyStringTenant_IsAMisconfiguration_ForReadsAndCommits()
    {
        using var context = new DocumentContext(_documents.Options, () => "");
        context.Database.EnsureCreated();

        var read = () => new EfRepository<Document, int>(context).GetAll();
        read.Should().Throw<TenantViolationException>().WithMessage("*empty string*");

        context.Documents.Add(new Document());
        var commit = () => new TestUnitOfWork(context).Commit();
        commit.Should().Throw<TenantViolationException>().WithMessage("*empty string*");
    }

    [Fact]
    public void AnEmptyStringTenantId_OnAnAddedEntity_IsRejected()
    {
        using var context = new DocumentContext(_documents.Options, () => "acme");
        context.Database.EnsureCreated();
        context.Documents.Add(new Document { TenantId = "" });

        var commit = () => new TestUnitOfWork(context).Commit();

        commit.Should().Throw<TenantViolationException>().WithMessage("*empty string*");
    }

    // ── Case-insensitive keys ─────────────────────────────────────────────────

    [Fact]
    public void Update_WithAKeyThatDiffersOnlyInCase_FromAnotherTenantsRow_Throws_AndLeavesItUnchanged()
    {
        using var database = new SqliteDatabase();
        SeedSku(database, TenantB, "abc", "theirs");

        _tenant.Current = TenantA;
        var commit = () =>
        {
            using var context = new SkuContext(database.Options, _tenant);
            new EfRepository<Sku, string>(context).Update(new Sku { Id = "ABC", TenantId = TenantA, Name = "hijacked" });
            new TestUnitOfWork(context).Commit();
        };

        commit.Should().Throw<TenantViolationException>();
        using var host = _hostScope.BeginHostScope();
        using var read = new SkuContext(database.Options, _tenant);
        read.Skus.Single().Name.Should().Be("theirs");
    }

    [Fact]
    public void HardDelete_WithAKeyThatDiffersOnlyInCase_FromAnotherTenantsRow_Throws_AndKeepsIt()
    {
        using var database = new SqliteDatabase();
        SeedSku(database, TenantB, "abc", "theirs");

        _tenant.Current = TenantA;
        var commit = () =>
        {
            using var context = new SkuContext(database.Options, _tenant);
            new EfRepository<Sku, string>(context).HardDelete(new Sku { Id = "ABC", TenantId = TenantA });
            new TestUnitOfWork(context).Commit();
        };

        commit.Should().Throw<TenantViolationException>();
        using var host = _hostScope.BeginHostScope();
        using var read = new SkuContext(database.Options, _tenant);
        read.Skus.Should().ContainSingle();
    }

    [Fact]
    public void Add_ReferencingAnotherTenantsRow_ByAKeyThatDiffersOnlyInCase_Throws()
    {
        using var database = new SqliteDatabase();
        SeedSku(database, TenantB, "abc", "theirs");

        _tenant.Current = TenantA;
        var commit = () =>
        {
            using var context = new SkuContext(database.Options, _tenant);
            context.StockLines.Add(new StockLine { SkuCode = "ABC" });
            new TestUnitOfWork(context).Commit();
        };

        commit.Should().Throw<TenantViolationException>().WithMessage("*SkuCode*");
    }

    [Fact]
    public void Update_OfAnOwnRow_ByAKeyThatDiffersOnlyInCase_Succeeds()
    {
        using var database = new SqliteDatabase();
        SeedSku(database, TenantA, "abc", "mine");

        _tenant.Current = TenantA;
        using (var context = new SkuContext(database.Options, _tenant))
        {
            new EfRepository<Sku, string>(context).Update(new Sku { Id = "ABC", TenantId = TenantA, Name = "renamed" });
            new TestUnitOfWork(context).Commit();
        }

        using var read = new SkuContext(database.Options, _tenant);
        read.Skus.Single().Name.Should().Be("renamed");
    }

    // ── Overrides ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ReadsAndCommits_InsideAnOverride_NeverAskTheTenantProvider()
    {
        // A background job: the provider reads a request that does not exist, so it must not be asked at all.
        using var context = new OrderContext(_database.Options, new ThrowingTenant());
        var repository = new EfRepository<Order, int>(context);
        var unitOfWork = new TestUnitOfWork(context);
        var first = new Order { Title = "sync" };
        var second = new Order { Title = "async" };

        using (new TenantScope<Guid>().Override(TenantA))
        {
            repository.Add(first);
            unitOfWork.Commit();

            repository.Add(second);
            await unitOfWork.CommitAsync(cancellationToken: TestContext.Current.CancellationToken);

            first.Title = "renamed";
            repository.Update(first);
            unitOfWork.Commit();

            repository.GetAll().Should().HaveCount(2);
        }

        first.TenantId.Should().Be(TenantA);
        second.TenantId.Should().Be(TenantA);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private OrderContext NewContext() => new(_database.Options, _tenant);

    private void Commit(Guid tenantId, Action<OrderContext> work)
    {
        _tenant.Current = tenantId;
        using var context = NewContext();
        work(context);
        new TestUnitOfWork(context).Commit();
    }

    private void SeedSku(SqliteDatabase database, Guid tenantId, string code, string name)
    {
        _tenant.Current = tenantId;
        using var context = new SkuContext(database.Options, _tenant);
        context.Database.EnsureCreated();
        context.Skus.Add(new Sku { Id = code, Name = name });
        new TestUnitOfWork(context).Commit();
    }

    private T Seed<T>(Guid tenantId, T entity)
        where T : class
    {
        Commit(tenantId, context => context.Add(entity));
        return entity;
    }
}
