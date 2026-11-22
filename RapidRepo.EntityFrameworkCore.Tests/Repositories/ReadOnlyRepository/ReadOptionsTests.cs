using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using RapidRepo.EntityFrameworkCore.Tests.Repositories.TestData;
using RapidRepo.Repositories;

namespace RapidRepo.EntityFrameworkCore.Tests.Repositories.ReadOnlyRepository;

/// <summary>
/// How the EF provider honours <see cref="SortOrder{TEntity}"/>, <see cref="ReadOptions"/> and the
/// <see cref="EfReadOptions{TEntity}"/> hints.
/// </summary>
public class ReadOptionsTests : BaseReadOnlyRepositoryTest
{
    private void Seed()
    {
        var manager = new Employee { FirstName = "Ada", LastName = "Lovelace" };
        _dbContext.Employees.AddRange(
            manager,
            new Employee { FirstName = "Alan", LastName = "Turing", Manager = manager },
            new Employee { FirstName = "Grace", LastName = "Hopper", Manager = manager },
            new Employee { FirstName = "Alonzo", LastName = "Church", DeletedAt = DateTime.UtcNow });
        _dbContext.SaveChanges();
        DetachAllEntities();
    }

    [Fact]
    public void GetAll_WithSortOrder_SortsByEveryKeyInTurn()
    {
        Seed();

        // Ada and Alan tie on the first key, so the second key decides between them.
        var result = _sut.GetAll(orderBy: SortOrder<Employee>.By(e => e.FirstName.Substring(0, 1)).ThenByDescending(e => e.LastName));

        result.Select(e => e.LastName).Should().Equal("Turing", "Lovelace", "Hopper");
    }

    [Fact]
    public void GetFirst_WithSortOrder_PicksTheFirstInThatOrder()
    {
        Seed();

        var result = _sut.GetFirst(orderBy: SortOrder<Employee>.ByDescending(e => e.LastName));

        result.LastName.Should().Be("Turing");
    }

    [Fact]
    public void GetAllPaged_WithSortOrder_PagesTheSortedSequence()
    {
        Seed();

        var result = _sut.GetAllPaged(orderBy: SortOrder<Employee>.By(e => e.LastName), pageIndex: 2, pageSize: 2);

        result.TotalCount.Should().Be(3);
        result.Results.Select(e => e.LastName).Should().Equal("Turing");
    }

    [Fact]
    public void GetAll_EfOrderBy_OverridesSortOrder()
    {
        Seed();

        var result = _sut.GetAll(
            orderBy: SortOrder<Employee>.By(e => e.LastName),
            options: new EfReadOptions<Employee> { OrderBy = q => q.OrderByDescending(e => e.LastName) });

        result.Select(e => e.LastName).Should().Equal("Turing", "Lovelace", "Hopper");
    }

    [Fact]
    public void GetAll_IncludeDeleted_ReturnsSoftDeletedEntities()
    {
        Seed();

        _sut.GetAll().Should().HaveCount(3);
        _sut.GetAll(options: new ReadOptions { IncludeDeleted = true }).Should().HaveCount(4);
    }

    [Fact]
    public void Any_And_Count_HonourIncludeDeleted()
    {
        Seed();
        var includeDeleted = new ReadOptions { IncludeDeleted = true };

        _sut.Any(e => e.LastName == "Church").Should().BeFalse();
        _sut.Any(e => e.LastName == "Church", includeDeleted).Should().BeTrue();
        _sut.Count(options: includeDeleted).Should().Be(4);
    }

    [Fact]
    public void GetAll_IgnoreAllQueryFilters_ReturnsSoftDeletedEntities()
    {
        Seed();

        _sut.GetAll(options: new EfReadOptions<Employee> { IgnoreAllQueryFilters = true }).Should().HaveCount(4);
    }

    [Fact]
    public void GetById_EfInclude_LoadsRelatedData()
    {
        Seed();
        var turingId = _dbContext.Employees.AsNoTracking().Single(e => e.LastName == "Turing").Id;

        var withoutInclude = _sut.GetById(turingId, new EfReadOptions<Employee> { Track = false });
        var withInclude = _sut.GetById(turingId, new EfReadOptions<Employee> { Track = false, Include = q => q.Include(e => e.Manager!) });

        withoutInclude!.Manager.Should().BeNull();
        withInclude!.Manager!.LastName.Should().Be("Lovelace");
    }

    [Fact]
    public void GetAll_ByDefault_TracksEntities()
    {
        Seed();

        var result = _sut.GetAll(e => e.LastName == "Turing").Single();

        _dbContext.Entry(result).State.Should().Be(EntityState.Unchanged);
    }

    [Fact]
    public void GetAll_TrackFalse_ReturnsDetachedEntities()
    {
        Seed();

        var result = _sut.GetAll(e => e.LastName == "Turing", options: new EfReadOptions<Employee> { Track = false }).Single();

        _dbContext.Entry(result).State.Should().Be(EntityState.Detached);
    }

    [Fact]
    public void EfReadOptions_ForAnotherEntityType_OnlyNeutralMembersApply()
    {
        Seed();

        // Track = false is meant for Company; for Employee only IncludeDeleted carries over.
        var options = new EfReadOptions<Company> { IncludeDeleted = true, Track = false };

        var result = _sut.GetAll(options: options).ToList();

        result.Should().HaveCount(4);
        result.Should().OnlyContain(e => _dbContext.Entry(e).State == EntityState.Unchanged);
    }

    [Fact]
    public void Query_AppliesOptions_AndComposesWithLinq()
    {
        Seed();

        var result = _sut
            .Query(new EfReadOptions<Employee> { IncludeDeleted = true, Track = false })
            .Where(e => e.FirstName.StartsWith("Al"))
            .OrderBy(e => e.LastName)
            .ToList();

        result.Select(e => e.LastName).Should().Equal("Church", "Turing");
        result.Should().OnlyContain(e => _dbContext.Entry(e).State == EntityState.Detached);
    }

    [Fact]
    public void Query_WithoutOptions_ExcludesSoftDeleted()
    {
        Seed();

        _sut.Query().Count().Should().Be(3);
    }
}
