using Microsoft.EntityFrameworkCore;
using RapidRepo.Repositories;
using RapidRepo.Tests.Repositories.TestData;

namespace RapidRepo.Tests.Repositories.ReadOnlyRepository.TestData;
internal class ReadOnlyEmployeeRepository : ReadOnlyRepository<Employee, int>, IReadOnlyEmployeeRepository
{
    public ReadOnlyEmployeeRepository(DbContext dbContext)
        : base(dbContext)
    {
    }
}
