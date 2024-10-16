using RapidRepo.Repositories;
using RapidRepo.Tests.TestData;

namespace Repository.Tests.TestData;

internal class EmployeeRepository : BaseRepository<Employee, int>, IEmployeeRepository
{
    public EmployeeRepository(TestDbContext dbContext)
        : base(dbContext)
    {
    }
}
