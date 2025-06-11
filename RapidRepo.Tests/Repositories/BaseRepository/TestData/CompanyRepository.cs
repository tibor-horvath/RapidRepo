using RapidRepo.Repositories;
using RapidRepo.Tests.Repositories.TestData;

namespace RapidRepo.Tests.Repositories.BaseRepository.TestData;

public class CompanyRepository : BaseRepository<Company, Guid>, ICompanyRepository
{
    public CompanyRepository(TestDbContext dbContext)
        : base(dbContext)
    {
    }
}