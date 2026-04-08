using Microsoft.EntityFrameworkCore;
using RapidRepo.Repositories;
using RapidRepo.Tests.Repositories.TestData;

namespace RapidRepo.Tests.Repositories.ReadOnlyRepository.TestData;

internal class ReadOnlyAccessTokenRepository : ReadOnlyRepository<AccessToken, string>
{
    public ReadOnlyAccessTokenRepository(DbContext dbContext)
        : base(dbContext)
    {
    }
}
