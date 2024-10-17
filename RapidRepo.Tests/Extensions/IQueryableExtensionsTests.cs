using AutoFixture;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using RapidRepo.Extensions;
using RapidRepo.Tests.Repositories.BaseRepository;
using Repository.Tests.TestData;

namespace RapidRepo.Tests.Extensions;
public class IQueryableExtensionsTests : BaseRepositoryTest
{
    [Fact]
    public void ApplyFilters_ShouldApplyCondition_WhenConditionIsNotNull()
    {
        // Arrange
        var testData = new List<Employee>
            {
                new() { Id = 1, FirstName = "John", LastName = "Doe" },
                new() { Id = 2, FirstName = "John", LastName = "Miller" },
                new() { Id = 3, FirstName = "Peter", LastName = "Parker" }
            }.AsQueryable();

        var mockSet = new Mock<DbSet<Employee>>();
        mockSet.As<IQueryable<Employee>>().Setup(m => m.Provider).Returns(testData.Provider);
        mockSet.As<IQueryable<Employee>>().Setup(m => m.Expression).Returns(testData.Expression);
        mockSet.As<IQueryable<Employee>>().Setup(m => m.ElementType).Returns(testData.ElementType);
        mockSet.As<IQueryable<Employee>>().Setup(m => m.GetEnumerator()).Returns(testData.GetEnumerator());

        // Act
        var result = mockSet.Object.ApplyFilters<Employee, int>(condition: e => e.FirstName == "John");

        // Assert
        result.Count().Should().Be(2);
        result.Should().OnlyContain(entity => entity.FirstName == "John");
    }

    [Fact]
    public void ApplyFilters_ShouldApplyOrderBy_WhenOrderByIsNotNull()
    {
        // Arrange
        var testData = new List<Employee>
            {
                new() { Id = 1, FirstName = "John", LastName = "Doe" },
                new() { Id = 2, FirstName = "John", LastName = "Miller" },
                new() { Id = 3, FirstName = "Peter", LastName = "Parker" }
            }.AsQueryable();

        var mockSet = new Mock<DbSet<Employee>>();
        mockSet.As<IQueryable<Employee>>().Setup(m => m.Provider).Returns(testData.Provider);
        mockSet.As<IQueryable<Employee>>().Setup(m => m.Expression).Returns(testData.Expression);
        mockSet.As<IQueryable<Employee>>().Setup(m => m.ElementType).Returns(testData.ElementType);
        mockSet.As<IQueryable<Employee>>().Setup(m => m.GetEnumerator()).Returns(testData.GetEnumerator());

        // Act
        var result = mockSet.Object.ApplyFilters<Employee, int>(orderBy: e => e.OrderBy(e => e.LastName));

        // Assert
        result.Count().Should().Be(3);
        result.Should().BeInAscendingOrder(e => e.LastName);
    }

    [Fact]
    public void ApplyFilters_ShouldApplyInclude_WhenIncludeIsNotNull()
    {
        // Arrange
        var testData = new List<Employee>
            {
                new() {
                    Id = 1,
                    FirstName = "John",
                    LastName = "Doe",
                    Manager = new Employee
                    {
                        Id = 4,
                        FirstName = "Tony",
                        LastName = "Stark"
                    }
                },
            }.AsQueryable();

        var mockSet = new Mock<DbSet<Employee>>();
        mockSet.As<IQueryable<Employee>>().Setup(m => m.Provider).Returns(testData.Provider);
        mockSet.As<IQueryable<Employee>>().Setup(m => m.Expression).Returns(testData.Expression);
        mockSet.As<IQueryable<Employee>>().Setup(m => m.ElementType).Returns(testData.ElementType);
        mockSet.As<IQueryable<Employee>>().Setup(m => m.GetEnumerator()).Returns(testData.GetEnumerator());

        // Act
        var result = mockSet.Object.ApplyFilters<Employee, int>(include: i => i.Include(e => e.Manager!));

        // Assert
        result.Count().Should().Be(1);
        result.First().Manager.Should().NotBeNull();
    }

    [Fact]
    public void ApplyFilters_ShouldNotApplyNoTracking_WhenTrackIsTrue()
    {
        // Arrange
        var employee1 = _fixture
            .Build<Employee>()
            .Without(e => e.Id)
            .With(e => e.FirstName, "FirstName1")
            .With(e => e.DeletedAt, DateTime.UtcNow)
            .Create();

        var employee2 = _fixture
            .Build<Employee>()
            .Without(e => e.Id)
            .With(e => e.FirstName, "FirstName2")
            .With(e => e.Manager, employee1)
            .Without(e => e.DeletedAt)
            .Create();

        _sut.Add(employee1);
        _sut.Add(employee2);
        _dbContext.SaveChanges();

        // Act
        var result = _sut.GetById(id: 2, track: true)!;

        // Assert
        bool tracked = _dbContext.Entry(result).State != EntityState.Detached;
        tracked.Should().BeTrue();
    }

    [Fact]
    public void ApplyFilters_ShouldApplyNoTracking_WhenTrackIsFalse()
    {
        // Arrange
        var employee1 = _fixture
            .Build<Employee>()
            .Without(e => e.Id)
            .With(e => e.FirstName, "FirstName1")
            .With(e => e.DeletedAt, DateTime.UtcNow)
            .Create();

        var employee2 = _fixture
            .Build<Employee>()
            .Without(e => e.Id)
            .With(e => e.FirstName, "FirstName2")
            .With(e => e.Manager, employee1)
            .Without(e => e.DeletedAt)
            .Create();

        _sut.Add(employee1);
        _sut.Add(employee2);
        _dbContext.SaveChanges();

        // Act
        var result = _sut.GetById(id: 2, track: false)!;

        // Assert
        bool tracked = _dbContext.Entry(result).State == EntityState.Detached;
        tracked.Should().BeTrue();
    }

    [Fact]
    public void IgnoreQueryFilters_ShouldReturnDeletedEmployees()
    {
        // Arrange
        var employee1 = _fixture
            .Build<Employee>()
            .Without(e => e.Id)
            .With(e => e.FirstName, "FirstName1")
            .With(e => e.DeletedAt, DateTime.UtcNow)
            .Create();

        var employee2 = _fixture
            .Build<Employee>()
            .Without(e => e.Id)
            .With(e => e.FirstName, "FirstName2")
            .With(e => e.Manager, employee1)
            .Without(e => e.DeletedAt)
            .Create();

        _sut.Add(employee1);
        _sut.Add(employee2);
        _dbContext.SaveChanges();

        // Act
        var result = _sut.GetAll(ignoreQueryFilters: true);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public void IgnoreQueryFilters_ShouldReturnNotDeletedEmployees()
    {
        // Arrange
        var employee1 = _fixture
            .Build<Employee>()
            .Without(e => e.Id)
            .With(e => e.FirstName, "FirstName1")
            .With(e => e.DeletedAt, DateTime.UtcNow)
            .Create();

        var employee2 = _fixture
            .Build<Employee>()
            .Without(e => e.Id)
            .With(e => e.FirstName, "FirstName2")
            .With(e => e.Manager, employee1)
            .Without(e => e.DeletedAt)
            .Create();

        _sut.Add(employee1);
        _sut.Add(employee2);
        _dbContext.SaveChanges();

        // Act
        var result = _sut.GetAll(ignoreQueryFilters: false);

        // Assert
        result.Should().HaveCount(1);
    }
}
