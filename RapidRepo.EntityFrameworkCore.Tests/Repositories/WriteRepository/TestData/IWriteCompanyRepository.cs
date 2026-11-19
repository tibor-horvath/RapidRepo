using RapidRepo.Repositories.Interfaces;
using RapidRepo.EntityFrameworkCore.Tests.Repositories.TestData;

namespace RapidRepo.EntityFrameworkCore.Tests.Repositories.WriteRepository.TestData;
internal interface IWriteCompanyRepository : IWriteRepository<Company, Guid>
{
}
