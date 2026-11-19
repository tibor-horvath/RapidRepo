using RapidRepo.Repositories;
using RapidRepo.EntityFrameworkCore.Tests.Repositories.TestData;

namespace RapidRepo.EntityFrameworkCore.Tests.Repositories.BaseRepository.TestData;

public class CompanyRepository : EfRepository<Company, Guid>, ICompanyRepository
{
    public CompanyRepository(TestDbContext dbContext)
        : base(dbContext)
    {
    }
}