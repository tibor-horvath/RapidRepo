using RapidRepo.Entities;

namespace Repository.Tests.TestData;

internal class Employee : BaseEntity<int>
{
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public DateTime DateOfBirth { get; set; }

    public int Salary { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public Employee? Manager { get; set; }
}
