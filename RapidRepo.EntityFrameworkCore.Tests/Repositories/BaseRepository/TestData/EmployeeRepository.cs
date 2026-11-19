using RapidRepo.Repositories;
using RapidRepo.EntityFrameworkCore.Tests.Repositories.TestData;

namespace RapidRepo.EntityFrameworkCore.Tests.Repositories.BaseRepository.TestData;

public class EmployeeRepository : EfRepository<Employee, int>, IEmployeeRepository
{
    public EmployeeRepository(TestDbContext dbContext)
        : base(dbContext)
    {
    }
}
