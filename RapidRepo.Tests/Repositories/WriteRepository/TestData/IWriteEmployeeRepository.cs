using RapidRepo.Repositories.Interfaces;
using RapidRepo.Tests.Repositories.TestData;

namespace RapidRepo.Tests.Repositories.WriteRepository.TestData;
internal interface IWriteEmployeeRepository : IWriteRepository<Employee, int>
{
}
