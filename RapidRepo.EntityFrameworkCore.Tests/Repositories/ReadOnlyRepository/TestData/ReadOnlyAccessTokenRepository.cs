using Microsoft.EntityFrameworkCore;
using RapidRepo.EntityFrameworkCore;
using RapidRepo.EntityFrameworkCore.Tests.Repositories.TestData;

namespace RapidRepo.EntityFrameworkCore.Tests.Repositories.ReadOnlyRepository.TestData;

internal class ReadOnlyAccessTokenRepository : EfReadOnlyRepository<AccessToken, string>
{
    public ReadOnlyAccessTokenRepository(DbContext dbContext)
        : base(dbContext)
    {
    }
}
