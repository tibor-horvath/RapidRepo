using RapidRepo.Repositories;
using RapidRepo.Tests.Repositories.TestData;

namespace RapidRepo.Tests.Repositories.BaseRepository.TestData;

public class EmployeeRepository : BaseRepository<Employee, int>, IEmployeeRepository
{
    public EmployeeRepository(TestDbContext dbContext)
        : base(dbContext)
    {
    }
}
