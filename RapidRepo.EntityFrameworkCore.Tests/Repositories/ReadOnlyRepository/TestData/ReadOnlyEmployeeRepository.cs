using Microsoft.EntityFrameworkCore;
using RapidRepo.EntityFrameworkCore;
using RapidRepo.EntityFrameworkCore.Tests.Repositories.TestData;

namespace RapidRepo.EntityFrameworkCore.Tests.Repositories.ReadOnlyRepository.TestData;
internal class ReadOnlyEmployeeRepository : EfReadOnlyRepository<Employee, int>, IReadOnlyEmployeeRepository
{
    public ReadOnlyEmployeeRepository(DbContext dbContext)
        : base(dbContext)
    {
    }
}
