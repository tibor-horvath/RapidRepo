using RapidRepo.Repositories.Interfaces;
using RapidRepo.Tests.Repositories.TestData;

namespace RapidRepo.Tests.Repositories.BaseRepository.TestData;

public interface IEmployeeRepository : IRepository<Employee, int>
{
}
