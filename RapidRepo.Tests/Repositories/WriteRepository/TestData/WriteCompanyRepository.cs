using Microsoft.EntityFrameworkCore;
using RapidRepo.Repositories;
using RapidRepo.Tests.Repositories.TestData;

namespace RapidRepo.Tests.Repositories.WriteRepository.TestData;
internal class WriteCompanyRepository : WriteRepository<Company, Guid>, IWriteCompanyRepository
{
    public WriteCompanyRepository(DbContext dbContext)
        : base(dbContext)
    {
    }
}
