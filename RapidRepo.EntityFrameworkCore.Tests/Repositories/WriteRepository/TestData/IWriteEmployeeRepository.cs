using RapidRepo.Repositories.Interfaces;
using RapidRepo.EntityFrameworkCore.Tests.Repositories.TestData;

namespace RapidRepo.EntityFrameworkCore.Tests.Repositories.WriteRepository.TestData;
internal interface IWriteEmployeeRepository : IWriteRepository<Employee, int>
{
}
