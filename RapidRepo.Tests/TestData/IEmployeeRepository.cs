using RapidRepo.Repositories.Interfaces;

namespace Repository.Tests.TestData;

internal interface IEmployeeRepository : IRepository<Employee, int>
{
}
