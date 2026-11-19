using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RapidRepo.EntityFrameworkCore.Tests.Repositories.BaseRepository.TestData;
using RapidRepo.EntityFrameworkCore.Tests.Repositories.TestData;
using RapidRepo.Repositories;

namespace RapidRepo.EntityFrameworkCore.Tests.UnitOfWork;

/// <summary>
/// Audit stamping as the EF unit of work applies it: change kinds derived from the change tracker, one clock,
/// and stamps only on the soft-delete and restore transitions.
/// </summary>
public class AuditingTests : IDisposable
{
    private static readonly DateTime T1 = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime T2 = new(2026, 3, 8, 17, 30, 0, DateTimeKind.Utc);

    private readonly TestDbContext _dbContext;
    private readonly MutableTimeProvider _clock = new(T1);
    private readonly ClockedUnitOfWork _sut;
    private readonly EmployeeRepository _employees;

    public AuditingTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: $"AuditingTests-{Guid.NewGuid()}")
            .Options;

        _dbContext = new TestDbContext(options);
        _dbContext.Database.EnsureCreated();
        _sut = new ClockedUnitOfWork(_dbContext, _clock);
        _employees = new EmployeeRepository(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }

    private Employee AddCommitted(Guid user)
    {
        var employee = new Employee { FirstName = "Ada", LastName = "Lovelace" };
        _employees.Add(employee);
        _sut.Commit(user);
        return employee;
    }

    [Fact]
    public void Commit_StampsAddedEntities_WithTheConfiguredClock()
    {
        var user = Guid.NewGuid();

        var employee = AddCommitted(user);

        employee.CreatedAt.Should().Be(T1);
        employee.CreatedBy.Should().Be(user);
    }

    [Fact]
    public void Commit_AfterRepositoryDelete_StampsDeletionAtCommitTime()
    {
        var creator = Guid.NewGuid();
        var deleter = Guid.NewGuid();
        var employee = AddCommitted(creator);

        _employees.Delete(employee);
        _clock.UtcNow = T2;
        _sut.Commit(deleter);

        employee.DeletedAt.Should().Be(T2, "the commit, not the Delete call, decides the deletion time");
        employee.DeletedBy.Should().Be(deleter);
        employee.ModifiedBy.Should().Be(deleter);
    }

    [Fact]
    public void Commit_EditingAnAlreadyDeletedEntity_KeepsTheOriginalDeletionStamp()
    {
        // Regression: 8.x re-stamped DeletedAt/DeletedBy on every save of a modified, already deleted entity.
        var deleter = Guid.NewGuid();
        var editor = Guid.NewGuid();
        var employee = AddCommitted(deleter);
        _employees.Delete(employee);
        _sut.Commit(deleter);

        _clock.UtcNow = T2;
        var reloaded = _employees.GetById(employee.Id, new ReadOptions { IncludeDeleted = true })!;
        reloaded.FirstName = "Augusta";
        _employees.Update(reloaded);
        _sut.Commit(editor);

        var stored = _dbContext.Employees.IgnoreQueryFilters().AsNoTracking().Single(e => e.Id == employee.Id);
        stored.DeletedAt.Should().Be(T1);
        stored.DeletedBy.Should().Be(deleter);
        stored.ModifiedAt.Should().Be(T2);
        stored.ModifiedBy.Should().Be(editor);
    }

    [Fact]
    public void Commit_AfterRestore_ClearsDeletionMarks_AndStampsModification()
    {
        var deleter = Guid.NewGuid();
        var restorer = Guid.NewGuid();
        var employee = AddCommitted(deleter);
        _employees.Delete(employee);
        _sut.Commit(deleter);

        _clock.UtcNow = T2;
        _employees.RestoreById(employee.Id);
        _sut.Commit(restorer);

        var stored = _dbContext.Employees.AsNoTracking().Single(e => e.Id == employee.Id);
        stored.DeletedAt.Should().BeNull();
        stored.DeletedBy.Should().BeNull();
        stored.ModifiedAt.Should().Be(T2);
        stored.ModifiedBy.Should().Be(restorer);
    }

    [Fact]
    public async Task CommitAsync_StampsEveryEntryWithTheSameTime()
    {
        _employees.Add(new Employee { FirstName = "Alan", LastName = "Turing" });
        _employees.Add(new Employee { FirstName = "Grace", LastName = "Hopper" });

        await _sut.CommitAsync(Guid.NewGuid());

        _dbContext.Employees.AsNoTracking().Select(e => e.CreatedAt).Distinct().Should().Equal(T1);
    }

    [Fact]
    public void Commit_DoesNotDisposeTheContext()
    {
        AddCommitted(Guid.NewGuid());

        var act = () => _dbContext.Employees.Count();

        act.Should().NotThrow();
        typeof(EfUnitOfWork<Guid>).Should().NotImplement<IDisposable>("the unit of work does not own the context");
    }

    private sealed class ClockedUnitOfWork(DbContext dbContext, TimeProvider clock)
        : EfUnitOfWork<Guid>(dbContext, Guid.Empty, serviceProvider: null, timeProvider: clock);

    private sealed class MutableTimeProvider(DateTime utcNow) : TimeProvider
    {
        public DateTime UtcNow { get; set; } = utcNow;

        public override DateTimeOffset GetUtcNow() => new(UtcNow);
    }
}
