using Microsoft.EntityFrameworkCore;
using RapidRepo.Repositories;
using RapidRepo.Tests.Repositories.TestData;

namespace RapidRepo.Tests.Repositories.WriteRepository.TestData;
internal class WriteEmployeeRepository : WriteRepository<Employee, int>, IWriteEmployeeRepository
{
    public WriteEmployeeRepository(DbContext dbContext)
        : base(dbContext)
    {
    }
}
