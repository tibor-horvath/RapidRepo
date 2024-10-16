using RapidRepo.Repositories;

namespace RapidRepo.Tests.TestData;

internal class CompanyRepository : BaseRepository<Company, Guid>, ICompanyRepository
{
    public CompanyRepository(TestDbContext dbContext)
        : base(dbContext)
    {
    }
}