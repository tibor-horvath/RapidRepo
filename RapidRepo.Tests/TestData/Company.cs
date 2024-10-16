using Repository.Tests.TestData;
using RapidRepo.Entities;
using RapidRepo.Entities.Interfaces;

namespace RapidRepo.Tests.TestData;

internal class Company : BaseEntity<Guid>, IAuditableEntity<Guid>
{
    public required string Name { get; set; }
    public IList<Employee> Employees { get; set; } = [];
    public Guid CreatedBy { get; set; }
    public Guid? ModifiedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
}
