using RapidRepo.Entities;

namespace RapidRepo.EntityFrameworkCore.Tests.Repositories.TestData;

public class AccessToken : BaseEntity<string>
{
    public required string Value { get; set; }
}
