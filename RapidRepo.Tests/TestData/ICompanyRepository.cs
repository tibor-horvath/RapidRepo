using RapidRepo.Repositories.Interfaces;

namespace RapidRepo.Tests.TestData;

public interface ICompanyRepository : IRepository<Company, Guid>
{
}
