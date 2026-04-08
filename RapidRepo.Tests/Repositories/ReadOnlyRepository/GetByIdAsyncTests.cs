using FluentAssertions;
using RapidRepo.Tests.Repositories.ReadOnlyRepository.TestData;
using RapidRepo.Tests.Repositories.TestData;

namespace RapidRepo.Tests.Repositories.ReadOnlyRepository;
public class GetByIdAsyncTests : BaseReadOnlyRepositoryTest
{
    [Fact]
    public async Task GetByIdAsync_ShouldReturnEntityWithMatchingId()
    {
        // Arrange
        var employee = new Employee
        {
            FirstName = "TestFirstName",
            LastName = "TestLastName",
        };

        _dbContext.Employees.Add(employee);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Act
        var result = await _sut.GetByIdAsync(employee.Id);

        // Assert
        result.Should().BeEquivalentTo(employee);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnEntityWithMatchingIdWithFirstName()
    {
        // Arrange
        var fistNameExpected = "TestFirstName";
        var testEmployeeId = 78;

        var employee = new Employee
        {
            Id = testEmployeeId,
            FirstName = fistNameExpected,
            LastName = "TestLastName",
        };

        var employees = new List<Employee>
        {
            new Employee { Id = 1, FirstName = "FirstName1", LastName = "LastName1" },
            new Employee { Id = 2, FirstName = "FirstName2", LastName = "LastName2" },
            new Employee { Id = 3, FirstName = "FirstName3", LastName = "LastName3" },
            new Employee { Id = 4, FirstName = "FirstName4", LastName = "LastName4" }
        };

        employees.Add(employee);

        _dbContext.Employees.AddRange(employees);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Act
        var result = await _sut.GetByIdAsync(id: testEmployeeId, selector: e => e.FirstName);

        // Assert
        result.Should().Be(fistNameExpected);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnEntityWithMatchingIdWithFirstAndLastName()
    {
        // Arrange
        var fistNameExpected = "TestFirstName";
        var lastNameExpected = "TestLastName";
        var testEmployeeId = 78;

        var employee = new Employee
        {
            Id = testEmployeeId,
            FirstName = fistNameExpected,
            LastName = lastNameExpected
        };

        var employees = new List<Employee>
        {
            new Employee { Id = 1, FirstName = "FirstName1", LastName = "LastName1" },
            new Employee { Id = 2, FirstName = "FirstName2", LastName = "LastName2" },
            new Employee { Id = 3, FirstName = "FirstName3", LastName = "LastName3" },
            new Employee { Id = 4, FirstName = "FirstName4", LastName = "LastName4" }
        };

        employees.Add(employee);

        _dbContext.Employees.AddRange(employees);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Act
        var result = await _sut.GetByIdAsync(id: testEmployeeId, selector: e => new { e.FirstName, e.LastName });

        // Assert
        result.Should().BeEquivalentTo(new { FirstName = fistNameExpected, LastName = lastNameExpected });
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnEntity_WhenStringKeyIsUsed()
    {
        // Arrange
        var repository = new ReadOnlyAccessTokenRepository(_dbContext);
        var token = new AccessToken
        {
            Id = "token-async-1",
            Value = "abc123"
        };

        _dbContext.AccessTokens.Add(token);
        await _dbContext.SaveChangesAsync();
        DetachAllEntities();

        // Act
        var result = await repository.GetByIdAsync(token.Id);

        // Assert
        result.Should().BeEquivalentTo(token);
    }

    [Fact]
    public async Task GetByIdAsyncSelector_ShouldReturnValue_WhenStringKeyIsUsed()
    {
        // Arrange
        var repository = new ReadOnlyAccessTokenRepository(_dbContext);
        var token = new AccessToken
        {
            Id = "token-async-2",
            Value = "xyz789"
        };

        _dbContext.AccessTokens.Add(token);
        await _dbContext.SaveChangesAsync();
        DetachAllEntities();

        // Act
        var result = await repository.GetByIdAsync(id: token.Id, selector: t => t.Value);

        // Assert
        result.Should().Be(token.Value);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldThrowArgumentNullException_WhenIdIsNull()
    {
        // Arrange
        var repository = new ReadOnlyAccessTokenRepository(_dbContext);

        // Act
        var act = async () => await repository.GetByIdAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GetByIdAsyncSelector_ShouldThrowArgumentNullException_WhenIdIsNull()
    {
        // Arrange
        var repository = new ReadOnlyAccessTokenRepository(_dbContext);

        // Act
        var act = async () => await repository.GetByIdAsync(id: null!, selector: t => t.Value);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GetByIdAsyncSelector_ShouldThrowArgumentNullException_WhenSelectorIsNull()
    {
        var act = async () => await _sut.GetByIdAsync<string>(id: 1, selector: null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
