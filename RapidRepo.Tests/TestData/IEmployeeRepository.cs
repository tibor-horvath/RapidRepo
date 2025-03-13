using RapidRepo.Repositories.Interfaces;

namespace Repository.Tests.TestData;

public interface IEmployeeRepository : IRepository<Employee, int>
{
}
