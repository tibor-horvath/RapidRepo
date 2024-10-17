using AutoFixture;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RapidRepo.Tests.TestData;
using Repository.Tests.TestData;
using System.Linq.Expressions;

namespace Repository.Tests.Repositories;

public class BaseRepositoryTests : IDisposable
{
    private readonly IFixture _fixture;

    private TestDbContext _dbContext;
    private EmployeeRepository _sut;


    public BaseRepositoryTests()
    {
        _fixture = new Fixture();

        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());

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
        var employee = _fixture
            .Build<Employee>()
            .Without(e => e.Id)
            .Create();

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

        var employees = _fixture
        .Build<Employee>()
        .Without(e => e.Id)
        .CreateMany(numberOfEmployeesToAdd);



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
        var employee = _fixture
            .Build<Employee>()
            .Without(e => e.Id)
            .Create();

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
        var employee = _fixture
            .Build<Employee>()
            .Without(e => e.Id)
            .Create();

        _sut.Add(employee);
        _dbContext.SaveChanges();

        // Act
        var result = _sut.Any(e => e.Id == employee.Id + 1);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async void AnyAsync_ShouldReturnTrue_WhenConditionIsMet()
    {
        // Arrange
        var employee = _fixture
            .Build<Employee>()
            .Without(e => e.Id)
            .Create();

        _sut.Add(employee);
        _dbContext.SaveChanges();

        // Act
        var result = await _sut.AnyAsync(e => e.Id == employee.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async void AnyAsync_ShouldReturnFalse_WhenConditionIsNotMet()
    {
        // Arrange
        var employee = _fixture
            .Build<Employee>()
            .Without(e => e.Id)
            .Create();

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
        var employee1 = _fixture
            .Build<Employee>()
            .Without(e => e.Id)
            .Create();

        var employee2 = _fixture
            .Build<Employee>()
            .Without(e => e.Id)
            .Create();

        _sut.Add(employee1);
        _sut.Add(employee2);
        _dbContext.SaveChanges();

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
        var employee = _fixture
            .Build<Employee>()
            .Without(e => e.Id)
            .Create();

        _sut.Add(employee);
        _dbContext.SaveChanges();

        Expression<Func<Employee, bool>> condition = e => e.Id == employee.Id + 1;

        // Act
        var result = _sut.Count(condition);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async void CountAsync_ShouldReturnCorrectCount_WhenConditionIsMet()
    {
        // Arrange
        var employee1 = _fixture
            .Build<Employee>()
            .Without(e => e.Id)
            .Create();

        var employee2 = _fixture
            .Build<Employee>()
            .Without(e => e.Id)
            .Create();

        _sut.Add(employee1);
        _sut.Add(employee2);
        _dbContext.SaveChanges();

        Expression<Func<Employee, bool>> condition = e => e.Id == employee1.Id || e.Id == employee2.Id;

        // Act
        var result = await _sut.CountAsync(condition);

        // Assert
        result.Should().Be(2);
    }

    [Fact]
    public async void CountAsync_ShouldReturnZero_WhenConditionIsNotMet()
    {
        // Arrange
        var employee = _fixture
            .Build<Employee>()
            .Without(e => e.Id)
            .Create();

        _sut.Add(employee);
        _dbContext.SaveChanges();

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
        var employee1 = _fixture
            .Build<Employee>()
            .Without(e => e.Id)
            .Create();

        var employee2 = _fixture
            .Build<Employee>()
            .Without(e => e.Id)
            .Create();

        _sut.Add(employee1);
        _sut.Add(employee2);
        _dbContext.SaveChanges();

        // Act
        var result = _sut.GetFirst();

        // Assert
        result.Should().Be(employee1);
    }

    [Fact]
    public async void GetFirstAsync_ShouldReturnFirstEntity()
    {
        // Arrange
        var employee1 = _fixture
            .Build<Employee>()
            .Without(e => e.Id)
            .Create();

        var employee2 = _fixture
            .Build<Employee>()
            .Without(e => e.Id)
            .Create();

        _sut.Add(employee1);
        _sut.Add(employee2);
        _dbContext.SaveChanges();

        // Act
        var result = await _sut.GetFirstAsync();

        // Assert
        result.Should().Be(employee1);
    }

    #endregion

    #region GetFirstOrDefault, GetFirstOrDefaultAsync

    [Fact]
    public void GetFirstOrDefault_ShouldReturnFirstOrDefaultEntity()
    {
        // Arrange
        var employee1 = _fixture
            .Build<Employee>()
            .Without(e => e.Id)
            .Create();

        var employee2 = _fixture
            .Build<Employee>()
            .Without(e => e.Id)
            .Create();

        _sut.Add(employee1);
        _sut.Add(employee2);
        _dbContext.SaveChanges();

        // Act
        var result = _sut.GetFirstOrDefault();

        // Assert
        result.Should().Be(employee1);
    }

    [Fact]
    public async void GetFirstOrDefaultAsync_ShouldReturnFirstOrDefaultEntity()
    {
        // Arrange
        var employee1 = _fixture
            .Build<Employee>()
            .Without(e => e.Id)
            .Create();

        var employee2 = _fixture
            .Build<Employee>()
            .Without(e => e.Id)
            .Create();

        _sut.Add(employee1);
        _sut.Add(employee2);

        _dbContext.SaveChanges();

        // Act
        var result = await _sut.GetFirstOrDefaultAsync();

        // Assert
        result.Should().Be(employee1);
    }

    #endregion

    #region GetPaged, GetPagedAsync

    [Fact]
    public void GetPaged_ShouldReturnPagedEntities()
    {
        // Arrange
        var employee1 = _fixture
            .Build<Employee>()
            .Without(e => e.Id)
            .Create();

        var employee2 = _fixture
            .Build<Employee>()
            .Without(e => e.Id)
            .Create();

        var employee3 = _fixture
            .Build<Employee>()
            .Without(e => e.Id)
            .Create();

        _sut.Add(employee1);
        _sut.Add(employee2);
        _sut.Add(employee3);
        _dbContext.SaveChanges();

        // Act
        var result = _sut.GetAllPaged(pageIndex: 1, pageSize: 2);

        // Assert
        result.Results.Should().Contain(employee1);
        result.Results.Should().Contain(employee2);
        result.Results.Should().NotContain(employee3);
        result.TotalCount.Should().Be(3);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(2);
    }

    [Fact]
    public async void GetPagedAsync_ShouldReturnPagedEntities()
    {
        // Arrange
        var employee1 = _fixture
            .Build<Employee>()
            .Without(e => e.Id)
            .Create();

        var employee2 = _fixture
            .Build<Employee>()
            .Without(e => e.Id)
            .Create();

        var employee3 = _fixture
            .Build<Employee>()
            .Without(e => e.Id)
            .Create();

        _sut.Add(employee1);
        _sut.Add(employee2);
        _sut.Add(employee3);
        _dbContext.SaveChanges();

        // Act
        var result = await _sut.GetPagedAsync(pageIndex: 1, pageSize: 2);

        // Assert
        result.Results.Should().Contain(employee1);
        result.Results.Should().Contain(employee2);
        result.Results.Should().NotContain(employee3);
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
        var employee = _fixture
            .Build<Employee>()
            .Without(e => e.Id)
            .Create();

        _sut.Add(employee);
        _dbContext.SaveChanges();

        // Act
        var result = _sut.GetSingle();

        // Assert
        result.Should().Be(employee);
    }

    [Fact]
    public async void GetSingleAsync_ShouldReturnSingleEntity()
    {
        // Arrange
        var employee = _fixture
            .Build<Employee>()
            .Without(e => e.Id)
            .Create();

        _sut.Add(employee);
        _dbContext.SaveChanges();

        // Act
        var result = await _sut.GetSingleAsync();

        // Assert
        result.Should().Be(employee);
    }

    #endregion

    #region GetSingleOrDefault, GetSingleOrDefaultAsync

    [Fact]
    public void GetSingleOrDefault_ShouldReturnSingleOrDefaultEntity()
    {
        // Arrange
        var employee = _fixture
            .Build<Employee>()
            .Without(e => e.Id)
            .Create();

        _sut.Add(employee);
        _dbContext.SaveChanges();

        // Act
        var result = _sut.GetSingleOrDefault();

        // Assert
        result.Should().Be(employee);
    }

    [Fact]
    public async void GetSingleOrDefaultAsync_ShouldReturnSingleOrDefaultEntity()
    {
        // Arrange
        var employee = _fixture
            .Build<Employee>()
            .Without(e => e.Id)
            .Create();

        _sut.Add(employee);
        _dbContext.SaveChanges();

        // Act
        var result = await _sut.GetSingleOrDefaultAsync();

        // Assert
        result.Should().Be(employee);
    }

    #endregion

    #region Update

    [Fact]
    public void Update_ShouldUpdateEntityInDbContext()
    {
        // Arrange
        var employee = _fixture
            .Build<Employee>()
            .Without(e => e.Id)
            .Create();

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
}
