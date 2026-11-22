// The shims are obsolete by design; these tests exist to prove that 8.x declarations still compile and behave.
#pragma warning disable RRMIG001

using System.Reflection;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using RapidRepo.EntityFrameworkCore.Tests.Repositories.TestData;
using RapidRepo.Repositories;
using RapidRepo.Repositories.Interfaces;
using RapidRepo.UnitOfWork;

namespace RapidRepo.EntityFrameworkCore.Tests.Compatibility;

public class ObsoleteShimTests : IDisposable
{
    private readonly TestDbContext _dbContext;

    public ObsoleteShimTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: $"ObsoleteShimTests-{Guid.NewGuid()}")
            .Options;

        _dbContext = new TestDbContext(options);
        _dbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }

    public static TheoryData<Type, Type> ShimsAndReplacements => new()
    {
        { typeof(Repository<,>), typeof(EfRepository<,>) },
        { typeof(Repository<,,>), typeof(EfRepository<,>) },
        { typeof(BaseRepository<,>), typeof(EfRepository<,>) },
        { typeof(ReadOnlyRepository<,>), typeof(EfReadOnlyRepository<,>) },
        { typeof(WriteRepository<,>), typeof(EfWriteRepository<,>) },
        { typeof(BaseWriteRepository<,>), typeof(EfWriteRepository<,>) },
        { typeof(UnitOfWork<>), typeof(EfUnitOfWork<>) },
    };

    [Theory]
    [MemberData(nameof(ShimsAndReplacements))]
    public void Shim_DerivesFromItsReplacement(Type shim, Type replacement)
    {
        shim.BaseType!.GetGenericTypeDefinition().Should().Be(replacement);
    }

    [Theory]
    [MemberData(nameof(ShimsAndReplacements))]
    public void Shim_IsObsolete_WithSharedDiagnosticIdAndPointerToReplacement(Type shim, Type replacement)
    {
        var obsolete = shim.GetCustomAttribute<ObsoleteAttribute>();

        obsolete.Should().NotBeNull();
        obsolete!.IsError.Should().BeFalse("upgrading must not break the build");
        obsolete.DiagnosticId.Should().Be("RRMIG001");
        obsolete.Message.Should().Contain(replacement.Name[..replacement.Name.IndexOf('`')]);
    }

    [Fact]
    public void LegacyCustomRepository_AddAndCommit_PersistsAndAudits()
    {
        var repository = new LegacyEmployeeRepository(_dbContext);
        var unitOfWork = new LegacyUnitOfWork(_dbContext);
        var userId = Guid.NewGuid();

        repository.Add(new Employee { FirstName = "Jane", LastName = "Doe" });
        unitOfWork.Commit(userId);

        var saved = repository.GetAll().Should().ContainSingle().Subject;
        saved.CreatedBy.Should().Be(userId);
    }

    [Fact]
    public void LegacyRepository_IsUsableThroughTheContract()
    {
        IRepository<Employee, int> repository = new Repository<Employee, int>(_dbContext);

        repository.Add(new Employee { FirstName = "John", LastName = "Doe" });
        _dbContext.SaveChanges();

        repository.Count().Should().Be(1);
    }

    [Fact]
    public void LegacyUnitOfWork_Dispose_LeavesTheContextUsable()
    {
        var unitOfWork = new LegacyUnitOfWork(_dbContext);

        unitOfWork.Dispose();

        var act = () => _dbContext.Employees.Count();
        act.Should().NotThrow("the container, not the unit of work, owns the context");
    }

    private sealed class LegacyEmployeeRepository(DbContext dbContext) : BaseRepository<Employee, int>(dbContext);

    private sealed class LegacyUnitOfWork(DbContext dbContext) : UnitOfWork<Guid>(dbContext, Guid.Empty);
}
