using Repository.Tests.TestData;
using RapidRepo.UnitOfWork;

namespace RapidRepo.Tests.TestData;

internal class TestUnitOfWork : UnitOfWork<Guid>
{
    public IEmployeeRepository Employees { get; set; }

    public override Guid DefaultUserId => default;

    public TestUnitOfWork(
        TestDbContext dbContext,
        IEmployeeRepository employeeRepository)
        : base(dbContext)
    {
        Employees = employeeRepository;
    }
}
