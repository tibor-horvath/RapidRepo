using Microsoft.EntityFrameworkCore;
using Moq;
using RapidRepo.Tests.TestData;
using Repository.Tests.TestData;

namespace RapidRepo.Tests.UnitOfWork;
public class UnitOfWorkTests
{
    private TestUnitOfWork _sut;
    private Mock<IEmployeeRepository> _employeeRepositoryMock;
    private Mock<ICompanyRepository> _companyRepositoryMock;
    private TestDbContext _dbContext;

    public UnitOfWorkTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDatabase-{Guid.NewGuid()}")
            .EnableSensitiveDataLogging()
        .Options;

        _dbContext = new TestDbContext(options);

        _dbContext.Database.EnsureCreated();

        _employeeRepositoryMock = new Mock<IEmployeeRepository>();
        _companyRepositoryMock = new Mock<ICompanyRepository>();

        _sut = new TestUnitOfWork(
            _dbContext,
            _employeeRepositoryMock.Object,
            _companyRepositoryMock.Object);
    }

    [Fact]
    public void Commit_WhenNewEntityAdded_ShouldSaveChanges()
    {
        // Arrange
        var employee = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
        };

        _dbContext.Add(employee);
        var userKey = Guid.NewGuid();

        // Act
        _sut.Commit(userKey);

        // Assert
        var savedEmployee = _dbContext.Employees.FirstOrDefault();
        Assert.NotNull(savedEmployee);
        Assert.True((DateTime.UtcNow - savedEmployee.CreatedAt).TotalSeconds < 1);
        Assert.Equal(userKey, savedEmployee.CreatedBy);
    }

    [Fact]
    public void Commit_WhenNewEntityModified_ShouldSaveChanges()
    {
        // Arrange
        var employee = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
        };

        _dbContext.Add(employee);
        _dbContext.SaveChanges();

        var employeeCreatedAt = employee.CreatedAt;
        employee.FirstName = "Jane";
        var userKey = Guid.NewGuid();

        // Act
        _sut.Commit(userKey);

        // Assert
        var savedEmployee = _dbContext.Employees.FirstOrDefault();
        Assert.NotNull(savedEmployee);
        Assert.NotNull(savedEmployee.ModifiedAt);
        Assert.True((DateTime.UtcNow - savedEmployee.ModifiedAt.Value).TotalSeconds < 1);
        Assert.Equal(employeeCreatedAt, savedEmployee.CreatedAt);
        Assert.Equal(userKey, savedEmployee.ModifiedBy);
    }

    [Fact]
    public void Commit_WhenNewIDeletableEntityEntityModified_ShouldSaveChanges()
    {
        // Arrange
        var company = new Company
        {
            Name = "Company"
        };

        _dbContext.Add(company);
        var userKey = Guid.NewGuid();
        _sut.Commit(userKey);

        var companyCreatedAt = company.CreatedAt;
        company.Name = "Company Modified";

        // Act
        _sut.Commit(userKey);

        // Assert
        var modifiedCompany = _dbContext.Companies.FirstOrDefault();
        Assert.NotNull(modifiedCompany);
        Assert.NotNull(modifiedCompany.ModifiedAt);
        Assert.True((DateTime.UtcNow - modifiedCompany.ModifiedAt.Value).TotalSeconds < 1);
        Assert.Equal(companyCreatedAt, modifiedCompany.CreatedAt);
    }

    [Fact]
    public void Commit_WhenNewIAuditableEntityEntityModified_ShouldSaveChanges()
    {
        // Arrange
        var company = new Company
        {
            Name = "Company"
        };

        _dbContext.Add(company);
        var userKey = Guid.NewGuid();
        _sut.Commit(userKey);

        var companyCreatedAt = company.CreatedAt;
        company.Name = "Company Modified";

        // Act
        _sut.Commit(userKey);

        // Assert
        var modifiedCompany = _dbContext.Companies.FirstOrDefault();
        Assert.NotNull(modifiedCompany);
        Assert.NotNull(modifiedCompany.ModifiedAt);
        Assert.True((DateTime.UtcNow - modifiedCompany.ModifiedAt.Value).TotalSeconds < 1);
        Assert.Equal(companyCreatedAt, modifiedCompany.CreatedAt);
    }

    [Fact]
    public void Commit_WhenNewIDeletableEntityEntityDeleted_ShouldSaveChanges()
    {
        // Arrange
        var company = new Company
        {
            Name = "Company"
        };

        _dbContext.Add(company);
        var userKey = Guid.NewGuid();
        _sut.Commit(userKey);

        var companyCreatedAt = company.CreatedAt;
        company.DeletedAt = DateTime.UtcNow;

        // Act
        _sut.Commit(userKey);

        // Assert
        var deletedCompany = _dbContext.Companies.IgnoreQueryFilters().FirstOrDefault();
        Assert.NotNull(deletedCompany);
        Assert.NotNull(deletedCompany.DeletedAt);
        Assert.True((DateTime.UtcNow - deletedCompany.DeletedAt.Value).TotalSeconds < 1);
    }

    [Fact]
    public void Commit_WhenEntitySoftDeleted_ShouldSaveChanges()
    {
        // Arrange
        var employee = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
        };

        _dbContext.Add(employee);
        _dbContext.SaveChanges();
        var userKey = Guid.NewGuid();
        employee.DeletedAt = DateTime.UtcNow;

        // Act
        _sut.Commit(userKey);

        // Assert
        var deletedEmployee = _dbContext.Employees.IgnoreQueryFilters().FirstOrDefault();
        Assert.NotNull(deletedEmployee);
        Assert.NotNull(deletedEmployee.DeletedAt);
        Assert.True((DateTime.UtcNow - deletedEmployee.DeletedAt.Value).TotalSeconds < 1);
        Assert.Equal(userKey, deletedEmployee.DeletedBy);
    }

    [Fact]
    public async Task CommitAsync_WhenNewEntityAdded_ShouldSaveChanges()
    {
        // Arrange
        var employee = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
        };

        _dbContext.Add(employee);
        var userKey = Guid.NewGuid();

        // Act
        await _sut.CommitAsync(userKey);

        // Assert
        var savedEmployee = _dbContext.Employees.FirstOrDefault();
        Assert.NotNull(savedEmployee);
        Assert.True((DateTime.UtcNow - savedEmployee.CreatedAt).TotalSeconds < 1);
        Assert.Equal(userKey, savedEmployee.CreatedBy);
    }

    [Fact]
    public async Task CommitAsync_WhenNewEntityModified_ShouldSaveChanges()
    {
        // Arrange
        var employee = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
        };

        _dbContext.Add(employee);
        _dbContext.SaveChanges();

        var employeeCreatedAt = employee.CreatedAt;
        employee.FirstName = "Jane";
        var userKey = Guid.NewGuid();

        // Act
        await _sut.CommitAsync(userKey);

        // Assert
        var savedEmployee = _dbContext.Employees.FirstOrDefault();
        Assert.NotNull(savedEmployee);
        Assert.NotNull(savedEmployee.ModifiedAt);
        Assert.True((DateTime.UtcNow - savedEmployee.ModifiedAt.Value).TotalSeconds < 1);
        Assert.Equal(employeeCreatedAt, savedEmployee.CreatedAt);
        Assert.Equal(userKey, savedEmployee.ModifiedBy);
    }

    [Fact]
    public async Task CommitAsync_WhenEntitySoftDeleted_ShouldSaveChanges()
    {
        // Arrange
        var employee = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
        };

        _dbContext.Add(employee);
        _dbContext.SaveChanges();
        var userKey = Guid.NewGuid();
        employee.DeletedAt = DateTime.UtcNow;

        // Act
        await _sut.CommitAsync(userKey);

        // Assert
        var deletedEmployee = _dbContext.Employees.IgnoreQueryFilters().FirstOrDefault();
        Assert.NotNull(deletedEmployee);
        Assert.NotNull(deletedEmployee.DeletedAt);
        Assert.True((DateTime.UtcNow - deletedEmployee.DeletedAt.Value).TotalSeconds < 1);
    }
}
