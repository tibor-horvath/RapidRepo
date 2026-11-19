using Microsoft.EntityFrameworkCore;
using RapidRepo.EntityFrameworkCore;
using RapidRepo.EntityFrameworkCore.Tests.Repositories.TestData;

namespace RapidRepo.EntityFrameworkCore.Tests.Repositories.WriteRepository.TestData;
internal class WriteCompanyRepository : EfWriteRepository<Company, Guid>, IWriteCompanyRepository
{
    public WriteCompanyRepository(DbContext dbContext)
        : base(dbContext)
    {
    }
}
