using RapidRepo.Entities;

namespace RapidRepo.Tests.Repositories.TestData;

public class AccessToken : BaseEntity<string>
{
    public required string Value { get; set; }
}
