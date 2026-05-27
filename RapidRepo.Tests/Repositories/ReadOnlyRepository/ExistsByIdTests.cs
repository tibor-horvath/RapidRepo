using FluentAssertions;
using RapidRepo.Tests.Repositories.ReadOnlyRepository.TestData;
using RapidRepo.Tests.Repositories.TestData;

namespace RapidRepo.Tests.Repositories.ReadOnlyRepository;

public class ExistsByIdTests : BaseReadOnlyRepositoryTest
{
    [Fact]
    public void ExistsById_ShouldReturnTrue_WhenEntityExists()
    {
        // Arrange
        var employee = new Employee { FirstName = "John", LastName = "Doe", DateOfBirth = new DateTime(1990, 1, 1) };
        _dbContext.Employees.Add(employee);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Act
        var result = _sut.ExistsById(employee.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ExistsById_ShouldReturnFalse_WhenEntityDoesNotExist()
    {
        // Act
        var result = _sut.ExistsById(999);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ExistsById_ShouldReturnFalse_WhenEntityIsSoftDeleted()
    {
        // Arrange
        var employee = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
            DeletedAt = DateTime.UtcNow
        };
        _dbContext.Employees.Add(employee);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Act
        var result = _sut.ExistsById(employee.Id);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ExistsById_ShouldReturnTrue_WhenEntityIsSoftDeleted_AndIgnoreQueryFiltersIsTrue()
    {
        // Arrange
        var employee = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
            DeletedAt = DateTime.UtcNow
        };
        _dbContext.Employees.Add(employee);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Act
        var result = _sut.ExistsById(employee.Id, ignoreQueryFilters: true);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ExistsById_ShouldThrowArgumentNullException_WhenIdIsNull()
    {
        // Arrange
        var repository = new ReadOnlyAccessTokenRepository(_dbContext);

        // Act
        Action act = () => repository.ExistsById(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ExistsByIdAsync_ShouldReturnTrue_WhenEntityExists()
    {
        // Arrange
        var employee = new Employee { FirstName = "John", LastName = "Doe", DateOfBirth = new DateTime(1990, 1, 1) };
        _dbContext.Employees.Add(employee);
        await _dbContext.SaveChangesAsync();
        DetachAllEntities();

        // Act
        var result = await _sut.ExistsByIdAsync(employee.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsByIdAsync_ShouldReturnFalse_WhenEntityDoesNotExist()
    {
        // Act
        var result = await _sut.ExistsByIdAsync(999);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsByIdAsync_ShouldReturnFalse_WhenEntityIsSoftDeleted()
    {
        // Arrange
        var employee = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
            DeletedAt = DateTime.UtcNow
        };
        _dbContext.Employees.Add(employee);
        await _dbContext.SaveChangesAsync();
        DetachAllEntities();

        // Act
        var result = await _sut.ExistsByIdAsync(employee.Id);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsByIdAsync_ShouldReturnTrue_WhenEntityIsSoftDeleted_AndIgnoreQueryFiltersIsTrue()
    {
        // Arrange
        var employee = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
            DeletedAt = DateTime.UtcNow
        };
        _dbContext.Employees.Add(employee);
        await _dbContext.SaveChangesAsync();
        DetachAllEntities();

        // Act
        var result = await _sut.ExistsByIdAsync(employee.Id, ignoreQueryFilters: true);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsByIdAsync_ShouldThrowArgumentNullException_WhenIdIsNull()
    {
        // Arrange
        var repository = new ReadOnlyAccessTokenRepository(_dbContext);

        // Act
        Func<Task> act = () => repository.ExistsByIdAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
