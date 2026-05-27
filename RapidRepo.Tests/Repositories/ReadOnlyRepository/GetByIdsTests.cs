using FluentAssertions;
using RapidRepo.Tests.Repositories.ReadOnlyRepository.TestData;
using RapidRepo.Tests.Repositories.TestData;

namespace RapidRepo.Tests.Repositories.ReadOnlyRepository;

public class GetByIdsTests : BaseReadOnlyRepositoryTest
{
    [Fact]
    public void GetByIds_ShouldReturnMatchingEntities()
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

        var ids = new[] { employees[0].Id, employees[2].Id };

        // Act
        var result = _sut.GetByIds(ids).ToList();

        // Assert
        result.Should().HaveCount(2);
        result.Should().ContainSingle(e => e.FirstName == "Alice");
        result.Should().ContainSingle(e => e.FirstName == "Carol");
        result.Should().NotContain(e => e.FirstName == "Bob");
    }

    [Fact]
    public void GetByIds_ShouldReturnEmpty_WhenNoIdsMatch()
    {
        // Arrange
        var employee = new Employee { FirstName = "Alice", LastName = "Smith", DateOfBirth = new DateTime(1990, 1, 1) };
        _dbContext.Employees.Add(employee);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Act
        var result = _sut.GetByIds([999, 1000]);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetByIds_ShouldReturnEmpty_WhenIdsListIsEmpty()
    {
        // Act
        var result = _sut.GetByIds([]);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetByIds_ShouldThrowArgumentNullException_WhenIdsIsNull()
    {
        Action act = () => _sut.GetByIds(null!).ToList();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetByIds_WithSelector_ShouldReturnProjectedEntities()
    {
        // Arrange
        var employees = new[]
        {
            new Employee { FirstName = "Alice", LastName = "Smith", DateOfBirth = new DateTime(1990, 1, 1) },
            new Employee { FirstName = "Bob",   LastName = "Jones", DateOfBirth = new DateTime(1991, 2, 2) },
        };
        _dbContext.Employees.AddRange(employees);
        _dbContext.SaveChanges();
        DetachAllEntities();

        var ids = new[] { employees[0].Id, employees[1].Id };

        // Act
        var result = _sut.GetByIds(ids, e => e.FirstName).ToList();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain("Alice");
        result.Should().Contain("Bob");
    }

    [Fact]
    public void GetByIds_WithSelector_ShouldThrowArgumentNullException_WhenSelectorIsNull()
    {
        Action act = () => _sut.GetByIds<string>([1], selector: null!).ToList();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetByIds_ShouldRespectQueryFilters_AndExcludeSoftDeletedEntities()
    {
        // Arrange
        var employee = new Employee
        {
            FirstName = "Alice",
            LastName = "Smith",
            DateOfBirth = new DateTime(1990, 1, 1),
            DeletedAt = DateTime.UtcNow
        };
        _dbContext.Employees.Add(employee);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Act
        var result = _sut.GetByIds([employee.Id]);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetByIds_ShouldIncludeSoftDeletedEntities_WhenIgnoreQueryFiltersIsTrue()
    {
        // Arrange
        var employee = new Employee
        {
            FirstName = "Alice",
            LastName = "Smith",
            DateOfBirth = new DateTime(1990, 1, 1),
            DeletedAt = DateTime.UtcNow
        };
        _dbContext.Employees.Add(employee);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Act
        var result = _sut.GetByIds([employee.Id], ignoreQueryFilters: true).ToList();

        // Assert
        result.Should().HaveCount(1);
        result[0].FirstName.Should().Be("Alice");
    }

    [Fact]
    public async Task GetByIdsAsync_ShouldReturnMatchingEntities()
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

        var ids = new[] { employees[0].Id, employees[2].Id };

        // Act
        var result = (await _sut.GetByIdsAsync(ids)).ToList();

        // Assert
        result.Should().HaveCount(2);
        result.Should().ContainSingle(e => e.FirstName == "Alice");
        result.Should().ContainSingle(e => e.FirstName == "Carol");
    }

    [Fact]
    public async Task GetByIdsAsync_ShouldReturnEmpty_WhenIdsListIsEmpty()
    {
        // Act
        var result = await _sut.GetByIdsAsync([]);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByIdsAsync_ShouldThrowArgumentNullException_WhenIdsIsNull()
    {
        Func<Task> act = () => _sut.GetByIdsAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GetByIdsAsync_WithSelector_ShouldReturnProjectedEntities()
    {
        // Arrange
        var employees = new[]
        {
            new Employee { FirstName = "Alice", LastName = "Smith", DateOfBirth = new DateTime(1990, 1, 1) },
            new Employee { FirstName = "Bob",   LastName = "Jones", DateOfBirth = new DateTime(1991, 2, 2) },
        };
        _dbContext.Employees.AddRange(employees);
        await _dbContext.SaveChangesAsync();
        DetachAllEntities();

        var ids = new[] { employees[0].Id, employees[1].Id };

        // Act
        var result = (await _sut.GetByIdsAsync(ids, e => e.FirstName)).ToList();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain("Alice");
        result.Should().Contain("Bob");
    }

    [Fact]
    public async Task GetByIdsAsync_WithSelector_ShouldThrowArgumentNullException_WhenSelectorIsNull()
    {
        Func<Task> act = () => _sut.GetByIdsAsync<string>([1], selector: null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GetByIdsAsync_ShouldRespectQueryFilters_AndExcludeSoftDeletedEntities()
    {
        // Arrange
        var employee = new Employee
        {
            FirstName = "Alice",
            LastName = "Smith",
            DateOfBirth = new DateTime(1990, 1, 1),
            DeletedAt = DateTime.UtcNow
        };
        _dbContext.Employees.Add(employee);
        await _dbContext.SaveChangesAsync();
        DetachAllEntities();

        // Act
        var result = await _sut.GetByIdsAsync([employee.Id]);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByIdsAsync_ShouldIncludeSoftDeletedEntities_WhenIgnoreQueryFiltersIsTrue()
    {
        // Arrange
        var employee = new Employee
        {
            FirstName = "Alice",
            LastName = "Smith",
            DateOfBirth = new DateTime(1990, 1, 1),
            DeletedAt = DateTime.UtcNow
        };
        _dbContext.Employees.Add(employee);
        await _dbContext.SaveChangesAsync();
        DetachAllEntities();

        // Act
        var result = (await _sut.GetByIdsAsync([employee.Id], ignoreQueryFilters: true)).ToList();

        // Assert
        result.Should().HaveCount(1);
        result[0].FirstName.Should().Be("Alice");
    }
}
