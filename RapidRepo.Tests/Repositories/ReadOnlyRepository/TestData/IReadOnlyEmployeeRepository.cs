using RapidRepo.Repositories.Interfaces;
using RapidRepo.Tests.Repositories.TestData;

namespace RapidRepo.Tests.Repositories.ReadOnlyRepository.TestData;
internal interface IReadOnlyEmployeeRepository : IReadOnlyRepository<Employee, int>
{
}
