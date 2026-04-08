using RapidRepo.Repositories;
using RapidRepo.Tests.Repositories.TestData;

namespace RapidRepo.Tests.Repositories.BaseRepository.TestData;

public class AccessTokenRepository : BaseRepository<AccessToken, string>
{
    public AccessTokenRepository(TestDbContext dbContext)
        : base(dbContext)
    {
    }
}
