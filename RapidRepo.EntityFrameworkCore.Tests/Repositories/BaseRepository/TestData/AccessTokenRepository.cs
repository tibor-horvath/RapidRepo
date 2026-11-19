using RapidRepo.EntityFrameworkCore;
using RapidRepo.EntityFrameworkCore.Tests.Repositories.TestData;

namespace RapidRepo.EntityFrameworkCore.Tests.Repositories.BaseRepository.TestData;

public class AccessTokenRepository : EfRepository<AccessToken, string>
{
    public AccessTokenRepository(TestDbContext dbContext)
        : base(dbContext)
    {
    }
}
