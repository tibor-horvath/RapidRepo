using FluentAssertions;
using RapidRepo.Tests.Repositories.TestData;

namespace RapidRepo.Tests.Repositories.ReadOnlyRepository;

public class GetPagedSelectorTests : BaseReadOnlyRepositoryTest
{
    [Fact]
    public void GetAllPaged_WithSelector_ReturnsProjectedPage()
    {
        // Arrange
        var employees = new[]
        {
            new Employee { FirstName = "Alice", LastName = "Smith", DateOfBirth = new DateTime(1990, 1, 1) },
            new Employee { FirstName = "Bob",   LastName = "Jones", DateOfBirth = new DateTime(1991, 2, 2) },
            new Employee { FirstName = "Carol", LastName = "Brown", DateOfBirth = new DateTime(1992, 3, 3) },
        };
        _dbContext.Employees.AddRange(employees);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Act
        var result = _sut.GetAllPaged(e => new EmployeeSummary(e.FirstName, e.LastName), pageIndex: 1, pageSize: 2);

        // Assert
        result.Results.Should().HaveCount(2);
        result.TotalCount.Should().Be(3);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(2);
        result.Results.Should().ContainSingle(e => e.FirstName == "Alice" && e.LastName == "Smith");
        result.Results.Should().ContainSingle(e => e.FirstName == "Bob" && e.LastName == "Jones");
    }

    [Fact]
    public void GetAllPaged_WithSelector_SecondPage_ReturnsRemainingItems()
    {
        // Arrange
        var employees = new[]
        {
            new Employee { FirstName = "Alice", LastName = "Smith", DateOfBirth = new DateTime(1990, 1, 1) },
            new Employee { FirstName = "Bob",   LastName = "Jones", DateOfBirth = new DateTime(1991, 2, 2) },
            new Employee { FirstName = "Carol", LastName = "Brown", DateOfBirth = new DateTime(1992, 3, 3) },
        };
        _dbContext.Employees.AddRange(employees);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Act
        var result = _sut.GetAllPaged(e => new EmployeeSummary(e.FirstName, e.LastName), pageIndex: 2, pageSize: 2);

        // Assert
        result.Results.Should().HaveCount(1);
        result.TotalCount.Should().Be(3);
        result.Results.Should().ContainSingle(e => e.FirstName == "Carol" && e.LastName == "Brown");
    }

    [Fact]
    public void GetAllPaged_WithSelector_AndCondition_FiltersBeforePaging()
    {
        // Arrange
        var employees = new[]
        {
            new Employee { FirstName = "Alice", LastName = "Smith", DateOfBirth = new DateTime(1990, 1, 1), Salary = 1000 },
            new Employee { FirstName = "Bob",   LastName = "Jones", DateOfBirth = new DateTime(1991, 2, 2), Salary = 2000 },
            new Employee { FirstName = "Carol", LastName = "Brown", DateOfBirth = new DateTime(1992, 3, 3), Salary = 2000 },
        };
        _dbContext.Employees.AddRange(employees);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Act
        var result = _sut.GetAllPaged(
            e => new EmployeeSummary(e.FirstName, e.LastName),
            condition: e => e.Salary == 2000,
            pageIndex: 1,
            pageSize: 10);

        // Assert
        result.TotalCount.Should().Be(2);
        result.Results.Should().HaveCount(2);
        result.Results.Should().NotContain(e => e.FirstName == "Alice");
    }

    [Fact]
    public void GetAllPaged_WithSelector_ShouldThrowArgumentNullException_WhenSelectorIsNull()
    {
        Action act = () => _sut.GetAllPaged<EmployeeSummary>(selector: null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetAllPaged_WithSelector_ShouldThrowArgumentOutOfRangeException_WhenPageIndexIsZero()
    {
        Action act = () => _sut.GetAllPaged(e => new EmployeeSummary(e.FirstName, e.LastName), pageIndex: 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void GetAllPaged_WithSelector_ShouldThrowArgumentOutOfRangeException_WhenPageSizeIsZero()
    {
        Action act = () => _sut.GetAllPaged(e => new EmployeeSummary(e.FirstName, e.LastName), pageSize: 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task GetAllPagedAsync_WithSelector_ReturnsProjectedPage()
    {
        // Arrange
        var employees = new[]
        {
            new Employee { FirstName = "Alice", LastName = "Smith", DateOfBirth = new DateTime(1990, 1, 1) },
            new Employee { FirstName = "Bob",   LastName = "Jones", DateOfBirth = new DateTime(1991, 2, 2) },
            new Employee { FirstName = "Carol", LastName = "Brown", DateOfBirth = new DateTime(1992, 3, 3) },
        };
        _dbContext.Employees.AddRange(employees);
        await _dbContext.SaveChangesAsync();
        DetachAllEntities();

        // Act
        var result = await _sut.GetAllPagedAsync(e => new EmployeeSummary(e.FirstName, e.LastName), pageIndex: 1, pageSize: 2);

        // Assert
        result.Results.Should().HaveCount(2);
        result.TotalCount.Should().Be(3);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(2);
    }

    [Fact]
    public async Task GetAllPagedAsync_WithSelector_ShouldThrowArgumentNullException_WhenSelectorIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _sut.GetAllPagedAsync<EmployeeSummary>(selector: null!));
    }

    [Fact]
    public async Task GetAllPagedAsync_WithSelector_ShouldThrowArgumentOutOfRangeException_WhenPageIndexIsZero()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _sut.GetAllPagedAsync(e => new EmployeeSummary(e.FirstName, e.LastName), pageIndex: 0));
    }

    [Fact]
    public async Task GetAllPagedAsync_WithSelector_ShouldThrowArgumentOutOfRangeException_WhenPageSizeIsZero()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _sut.GetAllPagedAsync(e => new EmployeeSummary(e.FirstName, e.LastName), pageSize: 0));
    }
}

internal record EmployeeSummary(string FirstName, string LastName);
