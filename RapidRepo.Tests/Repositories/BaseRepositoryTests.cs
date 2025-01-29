using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RapidRepo.Tests.TestData;
using Repository.Tests.TestData;
using System.Linq.Expressions;

namespace Repository.Tests.Repositories;

public class BaseRepositoryTests : IDisposable
{
    private TestDbContext _dbContext;
    private EmployeeRepository _sut;


    public BaseRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDatabase-{Guid.NewGuid()}")
            .EnableSensitiveDataLogging()
            .Options;
        _dbContext = new TestDbContext(options);
        _sut = new EmployeeRepository(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    #region Add

    [Fact]
    public void Add_ShouldAddEntityToDbContext()
    {
        // Arrange       
        var employee = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
        };

        // Act
        _sut.Add(employee);
        _dbContext.SaveChanges();

        // Assert
        var addedEmployee = _dbContext.Employees.SingleOrDefault();
        addedEmployee.Should().NotBeNull();
        addedEmployee.Should().BeEquivalentTo(employee);
    }

    [Fact]
    public void Add_ShouldAddEntitiesToDbContext()
    {
        // Arrange
        var numberOfEmployeesToAdd = 5;

        var employees = new List<Employee>();

        for (var i = 0; i < numberOfEmployeesToAdd; i++)
        {
            employees.Add(new Employee
            {
                FirstName = "John",
                LastName = "Doe",
                DateOfBirth = new DateTime(1990, 1, 1)
            });
        }


        // Act
        _sut.Add(employees);
        _dbContext.SaveChanges();

        // Assert
        _dbContext.Employees.Count().Should().Be(numberOfEmployeesToAdd);
        _dbContext.Employees.Select(e => e.Id).Should().OnlyHaveUniqueItems();
    }

    #endregion

    #region Any, AnyAsync

    [Fact]
    public void Any_ShouldReturnTrue_WhenConditionIsMet()
    {
        // Arrange
        var employee = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
        };

        _sut.Add(employee);
        _dbContext.SaveChanges();

        // Act
        var result = _sut.Any(e => e.Id == employee.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Any_ShouldReturnFalse_WhenConditionIsNotMet()
    {
        // Arrange
        var employee = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
        };

        _sut.Add(employee);
        _dbContext.SaveChanges();

        // Act
        var result = _sut.Any(e => e.Id == employee.Id + 1);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task AnyAsync_ShouldReturnTrue_WhenConditionIsMet()
    {
        // Arrange
        var employee = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
        };

        _sut.Add(employee);
        _dbContext.SaveChanges();

        // Act
        var result = await _sut.AnyAsync(e => e.Id == employee.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task AnyAsync_ShouldReturnFalse_WhenConditionIsNotMet()
    {
        // Arrange
        var employee = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
        };

        _sut.Add(employee);
        _dbContext.SaveChanges();

        // Act
        var result = await _sut.AnyAsync(e => e.Id == employee.Id + 1);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Count, CountAsync

    [Fact]
    public void Count_ShouldReturnCorrectCount_WhenConditionIsMet()
    {
        // Arrange
        var employee1 = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
        };

        var employee2 = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
        };

        _sut.Add(employee1);
        _sut.Add(employee2);
        _dbContext.SaveChanges();

        DetachAllEntities();

        Expression<Func<Employee, bool>> condition = e => e.Id == employee1.Id || e.Id == employee2.Id;

        // Act
        var result = _sut.Count(condition);

        // Assert
        result.Should().Be(2);
    }

    [Fact]
    public void Count_ShouldReturnZero_WhenConditionIsNotMet()
    {
        // Arrange
        var employee = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
        };

        _sut.Add(employee);
        _dbContext.SaveChanges();

        DetachAllEntities();

        Expression<Func<Employee, bool>> condition = e => e.Id == employee.Id + 1;

        // Act
        var result = _sut.Count(condition);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task CountAsync_ShouldReturnCorrectCount_WhenConditionIsMet()
    {
        // Arrange
        var employee1 = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
        };

        var employee2 = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
        };

        _sut.Add(employee1);
        _sut.Add(employee2);
        _dbContext.SaveChanges();

        DetachAllEntities();

        Expression<Func<Employee, bool>> condition = e => e.Id == employee1.Id || e.Id == employee2.Id;

        // Act
        var result = await _sut.CountAsync(condition);

        // Assert
        result.Should().Be(2);
    }

    [Fact]
    public async Task CountAsync_ShouldReturnZero_WhenConditionIsNotMet()
    {
        // Arrange
        var employee = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
        };

        _sut.Add(employee);
        _dbContext.SaveChanges();

        DetachAllEntities();

        Expression<Func<Employee, bool>> condition = e => e.Id == employee.Id + 1;

        // Act
        var result = await _sut.CountAsync(condition);

        // Assert
        result.Should().Be(0);
    }

    #endregion

    #region GetFirst, GetFirstAsync

    [Fact]
    public void GetFirst_ShouldReturnFirstEntity()
    {
        // Arrange
        var employee1 = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
        };

        var employee2 = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
        };

        _sut.Add(employee1);
        _sut.Add(employee2);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Act
        var result = _sut.GetFirst();

        // Assert
        result.Should().BeEquivalentTo(employee1);
    }

    [Fact]
    public async Task GetFirstAsync_ShouldReturnFirstEntity()
    {
        // Arrange
        var employee1 = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
        };

        var employee2 = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
        };

        _sut.Add(employee1);
        _sut.Add(employee2);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Act
        var result = await _sut.GetFirstAsync();

        // Assert
        result.Should().BeEquivalentTo(employee1);
    }

    #endregion

    #region GetFirstOrDefault, GetFirstOrDefaultAsync

    [Fact]
    public void GetFirstOrDefault_ShouldReturnFirstOrDefaultEntity()
    {
        // Arrange
        var employee1 = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
        };

        var employee2 = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
        };

        _sut.Add(employee1);
        _sut.Add(employee2);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Act
        var result = _sut.GetFirstOrDefault();

        // Assert
        result.Should().BeEquivalentTo(employee1);
    }

    [Fact]
    public async Task GetFirstOrDefaultAsync_ShouldReturnFirstOrDefaultEntity()
    {
        // Arrange
        var employee1 = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
        };

        var employee2 = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
        };

        _sut.Add(employee1);
        _sut.Add(employee2);

        _dbContext.SaveChanges();
        DetachAllEntities();

        // Act
        var result = await _sut.GetFirstOrDefaultAsync();

        // Assert
        result.Should().BeEquivalentTo(employee1);
    }

    #endregion

    #region GetPaged, GetPagedAsync

    [Fact]
    public void GetPaged_ShouldReturnPagedEntities()
    {
        // Arrange
        var employee1 = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
        };

        var employee2 = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
        };

        var employee3 = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
        };

        _sut.Add(employee1);
        _sut.Add(employee2);
        _sut.Add(employee3);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Act
        var result = _sut.GetAllPaged(pageIndex: 1, pageSize: 2);

        // Assert
        result.Results.Should().ContainEquivalentOf(employee1);
        result.Results.Should().ContainEquivalentOf(employee2);
        result.Results.Should().NotContainEquivalentOf(employee3);
        result.TotalCount.Should().Be(3);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(2);
    }

    [Fact]
    public async Task GetPagedAsync_ShouldReturnPagedEntities()
    {
        // Arrange
        var employee1 = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
        };

        var employee2 = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
        };

        var employee3 = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
        };

        _sut.Add(employee1);
        _sut.Add(employee2);
        _sut.Add(employee3);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Act
        var result = await _sut.GetPagedAsync(pageIndex: 1, pageSize: 2);

        // Assert
        result.Results.Should().ContainEquivalentOf(employee1);
        result.Results.Should().ContainEquivalentOf(employee2);
        result.Results.Should().NotContainEquivalentOf(employee3);
        result.TotalCount.Should().Be(3);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(2);
    }

    #endregion

    #region GetSingle, GetSingleAsync

    [Fact]
    public void GetSingle_ShouldReturnSingleEntity()
    {
        // Arrange
        var employee = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
        };

        _sut.Add(employee);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Act
        var result = _sut.GetSingle();

        // Assert
        result.Should().BeEquivalentTo(employee);
    }

    [Fact]
    public async Task GetSingleAsync_ShouldReturnSingleEntity()
    {
        // Arrange
        var employee = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
        };

        _sut.Add(employee);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Act
        var result = await _sut.GetSingleAsync();

        // Assert
        result.Should().BeEquivalentTo(employee);
    }

    #endregion

    #region GetSingleOrDefault, GetSingleOrDefaultAsync

    [Fact]
    public void GetSingleOrDefault_ShouldReturnSingleOrDefaultEntity()
    {
        // Arrange
        var employee = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
        };

        _sut.Add(employee);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Act
        var result = _sut.GetSingleOrDefault();

        // Assert
        result.Should().BeEquivalentTo(employee);
    }

    [Fact]
    public async Task GetSingleOrDefaultAsync_ShouldReturnSingleOrDefaultEntity()
    {
        // Arrange
        var employee = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
        };

        _sut.Add(employee);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Act
        var result = await _sut.GetSingleOrDefaultAsync();

        // Assert
        result.Should().BeEquivalentTo(employee);
    }

    #endregion

    #region Update

    [Fact]
    public void Update_ShouldUpdateEntityInDbContext()
    {
        // Arrange
        var employee = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
        };

        _sut.Add(employee);
        _dbContext.SaveChanges();

        employee.FirstName = "Updated Name";

        // Act
        _sut.Update(employee);
        _dbContext.SaveChanges();

        // Assert
        var updatedEmployee = _dbContext.Employees.Find(employee.Id);
        updatedEmployee.Should().NotBeNull();
        updatedEmployee.Should().BeEquivalentTo(employee);
        updatedEmployee!.FirstName.Should().Be("Updated Name");
    }

    #endregion

    private void DetachAllEntities()
    {
        foreach (var entry in _dbContext.ChangeTracker.Entries())
        {
            entry.State = EntityState.Detached;
        }
    }
}
