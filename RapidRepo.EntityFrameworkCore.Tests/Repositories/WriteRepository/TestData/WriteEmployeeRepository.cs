using Microsoft.EntityFrameworkCore;
using RapidRepo.EntityFrameworkCore;
using RapidRepo.EntityFrameworkCore.Tests.Repositories.TestData;

namespace RapidRepo.EntityFrameworkCore.Tests.Repositories.WriteRepository.TestData;
internal class WriteEmployeeRepository : EfWriteRepository<Employee, int>, IWriteEmployeeRepository
{
    public WriteEmployeeRepository(DbContext dbContext)
        : base(dbContext)
    {
    }
}
