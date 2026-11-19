using Microsoft.EntityFrameworkCore;
using RapidRepo.EntityFrameworkCore;
using RapidRepo.EntityFrameworkCore.Tests.Repositories.TestData;

namespace RapidRepo.EntityFrameworkCore.Tests.Repositories.WriteRepository.TestData;

internal class WriteAccessTokenRepository : EfWriteRepository<AccessToken, string>
{
    public WriteAccessTokenRepository(DbContext dbContext)
        : base(dbContext)
    {
    }
}
