using RapidRepo.Entities;
using RapidRepo.Entities.Interfaces;

namespace RapidRepo.Tests.Repositories.TestData;

public class Company : BaseEntity<Guid>, IAuditableEntity, IDeletableEntity
{
    public required string Name { get; set; }
    public IList<Employee> Employees { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
