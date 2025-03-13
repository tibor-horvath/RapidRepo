using RapidRepo.Entities;
using RapidRepo.Entities.Interfaces;
using System.ComponentModel.DataAnnotations.Schema;

namespace Repository.Tests.TestData;

public class Employee : BaseEntity<int>, IAuditableDeletableEntity<Guid>
{
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public DateTime DateOfBirth { get; set; }
    [NotMapped]
    public int Age => DateTime.UtcNow.Year - DateOfBirth.Year;

    public int Salary { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public Employee? Manager { get; set; }
    public Guid? DeletedBy { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public Guid? ModifiedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
}
