using RapidRepo.Repositories.Interfaces;
using RapidRepo.EntityFrameworkCore.Tests.Repositories.TestData;

namespace RapidRepo.EntityFrameworkCore.Tests.Repositories.ReadOnlyRepository.TestData;
internal interface IReadOnlyEmployeeRepository : IReadOnlyRepository<Employee, int>
{
}
