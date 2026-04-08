using Microsoft.EntityFrameworkCore;
using RapidRepo.Repositories;
using RapidRepo.Tests.Repositories.TestData;

namespace RapidRepo.Tests.Repositories.WriteRepository.TestData;

internal class WriteAccessTokenRepository : WriteRepository<AccessToken, string>
{
    public WriteAccessTokenRepository(DbContext dbContext)
        : base(dbContext)
    {
    }
}
