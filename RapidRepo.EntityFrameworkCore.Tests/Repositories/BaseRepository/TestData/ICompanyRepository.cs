using RapidRepo.Repositories.Interfaces;
using RapidRepo.EntityFrameworkCore.Tests.Repositories.TestData;

namespace RapidRepo.EntityFrameworkCore.Tests.Repositories.BaseRepository.TestData;

public interface ICompanyRepository : IRepository<Company, Guid>
{
}
