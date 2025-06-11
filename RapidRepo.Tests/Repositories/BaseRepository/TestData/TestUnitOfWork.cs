using RapidRepo.Tests.Repositories.TestData;
using RapidRepo.UnitOfWork;

namespace RapidRepo.Tests.Repositories.BaseRepository.TestData;

internal class TestUnitOfWork : UnitOfWork<Guid>
{
    public IEmployeeRepository Employees { get; set; }
    public ICompanyRepository Companies { get; set; }

    public override Guid DefaultUserKey => default;

    public TestUnitOfWork(
        TestDbContext dbContext,
        IEmployeeRepository employeeRepository,
        ICompanyRepository companies)
        : base(dbContext)
    {
        Employees = employeeRepository;
        Companies = companies;
    }
}
